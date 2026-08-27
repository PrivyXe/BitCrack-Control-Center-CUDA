#include "CudaKangarooDevice.h"
#include "Logger.h"
#include "util.h"
#include "AddressUtil.h"
#include "cudaUtil.h"
#include <cstring>

void CudaKangarooDevice::cudaCall(cudaError_t err)
{
    if (err) {
        throw KeySearchException(cudaGetErrorString(err));
    }
}

int CudaKangarooDevice::bitLength(const secp256k1::uint256 &value) const
{
    for (int i = 7; i >= 0; i--) {
        if (value.v[i] != 0) {
            for (int b = 31; b >= 0; b--) {
                if (value.v[i] & (1u << b)) {
                    return i * 32 + b + 1;
                }
            }
        }
    }
    return 0;
}

CudaKangarooDevice::CudaKangarooDevice(int device, int threads, int blocks, unsigned int dpBits, int pointsPerThread)
{
    cuda::CudaDeviceInfo info;
    try {
        info = cuda::getDeviceInfo(device);
        _deviceName = info.name;
    } catch (cuda::CudaException ex) {
        throw KeySearchException(ex.msg);
    }

    if (threads <= 0 || threads % 32 != 0) {
        throw KeySearchException("The number of threads must be a multiple of 32");
    }

    if (blocks <= 0) {
        blocks = info.mpCount;
        while (threads > 256) {
            threads /= 2;
            blocks *= 2;
        }
    }

    _device = device;
    _threads = threads;
    _blocks = blocks;
    _pointsPerThread = (pointsPerThread <= 0) ? 32 : pointsPerThread;
    _dpBits = (dpBits == 0) ? 16 : dpBits;
    if (_dpBits > 31) {
        _dpBits = 31;
    }
    _dpMask = (1u << _dpBits) - 1u;
    _maxDpEntries = (unsigned int)_blocks * (unsigned int)_threads * 4u;
    if (_maxDpEntries < 4096u) {
        _maxDpEntries = 4096u;
    }
}

CudaKangarooDevice::~CudaKangarooDevice()
{
    freeDevice();
}

void CudaKangarooDevice::freeDevice()
{
    if (_d_state) {
        cudaFree(_d_state);
        _d_state = nullptr;
    }
    if (_d_dpBuffer) {
        cudaFree(_d_dpBuffer);
        _d_dpBuffer = nullptr;
    }
    if (_d_dpCount) {
        cudaFree(_d_dpCount);
        _d_dpCount = nullptr;
    }
    if (_d_foundKey) {
        cudaFree(_d_foundKey);
        _d_foundKey = nullptr;
    }
}

void CudaKangarooDevice::setTargetPublicKey(const secp256k1::ecpoint &pk)
{
    _targetPubKey = pk;
    _hasTarget = true;
}

void CudaKangarooDevice::setKeyRange(const secp256k1::uint256 &start, const secp256k1::uint256 &end)
{
    _startKey = start;
    _endKey = end;
}

void CudaKangarooDevice::buildJumpTable()
{
    KangarooJump jumps[64];
    memset(jumps, 0, sizeof(jumps));

    secp256k1::uint256 range = _endKey - _startKey;
    int rb = bitLength(range);
    if (rb < 2) {
        rb = 2;
    }

    // Optimal Pollard mean jump is ~ sqrt(range) / 2
    int targetMeanBits = (rb / 2) - 1;
    if (targetMeanBits < 1) targetMeanBits = 1;
    if (targetMeanBits > 120) targetMeanBits = 120;

    int minBits = targetMeanBits - 8;
    if (minBits < 1) minBits = 1;
    int maxBits = targetMeanBits + 8;
    if (maxBits > 120) maxBits = 120;

    secp256k1::ecpoint g = secp256k1::G();
    secp256k1::uint256 beta("7AE96A2B657C07106E64479EAC3434E99CF0497512F58995C1396C28719501EE");
    secp256k1::uint256 lambda("5363AD4CC05C30E0A5261C028812645A122E22EA20816678DF02967C1B23BD72");

    for (int i = 0; i < 32; i++) {
        int bits = minBits + ((i * (maxBits - minBits)) / 31);
        if (bits > maxBits) bits = maxBits;

        secp256k1::uint256 dist(1);
        for (int b = 1; b < bits; b++) {
            dist = dist.add(dist);
        }

        // Mix in pseudorandom odd offset based on index to ensure pseudo-random walk
        unsigned int oddOffset = (unsigned int)(2 * i + 1);
        dist = dist.add(oddOffset);

        secp256k1::ecpoint p = secp256k1::multiplyPoint(dist, g);

        dist.exportWords(jumps[i].distance, 8, secp256k1::uint256::BigEndian);
        p.x.exportWords(jumps[i].pointX, 8, secp256k1::uint256::BigEndian);
        p.y.exportWords(jumps[i].pointY, 8, secp256k1::uint256::BigEndian);

        // GLV jump equivalent: dist_glv = lambda * dist (mod N), Px_glv = beta * Px (mod P), Py_glv = Py
        secp256k1::uint256 distGLV = secp256k1::multiplyModN(dist, lambda);
        secp256k1::uint256 pointXGLV = secp256k1::multiplyModP(p.x, beta);

        distGLV.exportWords(jumps[i + 32].distance, 8, secp256k1::uint256::BigEndian);
        pointXGLV.exportWords(jumps[i + 32].pointX, 8, secp256k1::uint256::BigEndian);
        p.y.exportWords(jumps[i + 32].pointY, 8, secp256k1::uint256::BigEndian);
    }

    cudaCall(initKangarooJumpTable(jumps, 64));
}

void CudaKangarooDevice::initKangaroos()
{
    uint64_t total = (uint64_t)_blocks * (uint64_t)_threads;
    std::vector<KangarooState> hostState((size_t)total);
    memset(hostState.data(), 0, hostState.size() * sizeof(KangarooState));

    secp256k1::ecpoint g = secp256k1::G();
    secp256k1::uint256 range = _endKey - _startKey;
    secp256k1::uint256 step = range.div((uint32_t)total);
    if (step.isZero()) {
        step = secp256k1::uint256(1);
    }

    // Wild offset step: Start at 0, 1, 2... so wild kangaroos cover immediate offset neighborhood
    secp256k1::uint256 wildStep(1);

    Logger::log(LogLevel::Info, "Initializing " + util::formatThousands(total) + " tame/wild kangaroo pairs...");

    // Incremental init
    secp256k1::ecpoint stepPoint = secp256k1::multiplyPoint(step, g);
    secp256k1::ecpoint wildStepPoint = secp256k1::multiplyPoint(wildStep, g);

    secp256k1::ecpoint tamePoint = secp256k1::multiplyPoint(_startKey, g);
    secp256k1::uint256 tameScalar = _startKey;

    secp256k1::ecpoint wildPoint = _targetPubKey;
    secp256k1::uint256 wildOffset(0);

    double pct = 10.0;
    for (uint64_t i = 0; i < total; i++) {
        tameScalar.exportWords(hostState[i].tameDist, 8, secp256k1::uint256::BigEndian);
        tamePoint.x.exportWords(hostState[i].tameX, 8, secp256k1::uint256::BigEndian);
        tamePoint.y.exportWords(hostState[i].tameY, 8, secp256k1::uint256::BigEndian);
        hostState[i].tameZ[7] = 1;

        wildOffset.exportWords(hostState[i].wildDist, 8, secp256k1::uint256::BigEndian);
        wildPoint.x.exportWords(hostState[i].wildX, 8, secp256k1::uint256::BigEndian);
        wildPoint.y.exportWords(hostState[i].wildY, 8, secp256k1::uint256::BigEndian);
        hostState[i].wildZ[7] = 1;

        tamePoint = secp256k1::addPoints(tamePoint, stepPoint);
        tameScalar = tameScalar.add(step);
        wildPoint = secp256k1::addPoints(wildPoint, wildStepPoint);
        wildOffset = wildOffset.add(wildStep);

        if (((double)(i + 1) / (double)total) * 100.0 >= pct) {
            Logger::log(LogLevel::Info, util::format("%.1f%%", pct));
            pct += 10.0;
        }
    }

    Logger::log(LogLevel::Info, "Done");

    cudaCall(cudaMalloc(&_d_state, hostState.size() * sizeof(KangarooState)));
    cudaCall(cudaMemcpy(_d_state, hostState.data(), hostState.size() * sizeof(KangarooState), cudaMemcpyHostToDevice));
}

void CudaKangarooDevice::init(const secp256k1::uint256 &start, int /*compression*/, const secp256k1::uint256 & /*stride*/)
{
    if (!_hasTarget) {
        throw KeySearchException("Kangaroo mode requires a target public key (--pubkey)");
    }

    if (start.cmp(secp256k1::N) >= 0) {
        throw KeySearchException("Starting key is out of range");
    }

    _startKey = start;
    if (_endKey.isZero() || _endKey.cmp(_startKey) < 0) {
        throw KeySearchException("Kangaroo mode requires a valid --keyspace START:END");
    }

    cudaCall(cudaSetDevice(_device));
    cudaCall(cudaSetDeviceFlags(cudaDeviceScheduleBlockingSync));
    cudaCall(cudaDeviceSetCacheConfig(cudaFuncCachePreferL1));

    freeDevice();

    _detector.reset(new KangarooCollisionDetector(_startKey, _endKey, _targetPubKey));
    _h_dpBuffer.resize(_maxDpEntries);
    _results.clear();
    _iterations = 0;

    Logger::log(LogLevel::Info, "Kangaroo DP bits: " + util::format(_dpBits));

    buildJumpTable();
    initKangaroos();

    cudaCall(cudaMalloc(&_d_dpBuffer, _maxDpEntries * sizeof(DPEntry)));
    cudaCall(cudaMalloc(&_d_dpCount, sizeof(unsigned int)));
    cudaCall(cudaMalloc(&_d_foundKey, 8 * sizeof(unsigned int)));
    cudaCall(cudaMemset(_d_dpCount, 0, sizeof(unsigned int)));
    cudaCall(cudaMemset(_d_foundKey, 0, 8 * sizeof(unsigned int)));
}

void CudaKangarooDevice::setTargets(const std::set<KeySearchTarget> &targets)
{
    _targets.clear();
    for (auto i = targets.begin(); i != targets.end(); ++i) {
        _targets.push_back(hash160(i->value));
    }
}

void CudaKangarooDevice::doStep()
{
    cudaCall(cudaMemset(_d_dpCount, 0, sizeof(unsigned int)));

    cudaCall(runKangarooSearchKernelDP(
        _blocks,
        _threads,
        _d_state,
        _dpMask,
        _d_dpBuffer,
        _d_dpCount,
        _maxDpEntries,
        _d_foundKey,
        _pointsPerThread));

    cudaCall(cudaDeviceSynchronize());

    unsigned int dpCount = 0;
    cudaCall(cudaMemcpy(&dpCount, _d_dpCount, sizeof(unsigned int), cudaMemcpyDeviceToHost));

    if (dpCount > _maxDpEntries) {
        dpCount = _maxDpEntries;
    }

    if (dpCount > 0) {
        cudaCall(cudaMemcpy(_h_dpBuffer.data(), _d_dpBuffer, dpCount * sizeof(DPEntry), cudaMemcpyDeviceToHost));

        for (unsigned int i = 0; i < dpCount; i++) {
            secp256k1::uint256 foundKey;
            if (_detector->addAndCheckDP(_h_dpBuffer[i], foundKey)) {
                KeySearchResult r;
                r.privateKey = foundKey;
                r.publicKey = _targetPubKey;
                r.compressed = true;

                if (!_targets.empty()) {
                    memcpy(r.hash, _targets[0].h, sizeof(r.hash));
                } else {
                    unsigned int digest[5];
                    Hash::hashPublicKeyCompressed(_targetPubKey, digest);
                    memcpy(r.hash, digest, sizeof(r.hash));
                }

                _results.push_back(r);
                Logger::log(LogLevel::Info, "Kangaroo collision found after " + util::formatThousands(_iterations + 1) + " steps");
                break;
            }
        }
    }

    _iterations++;
}

size_t CudaKangarooDevice::getResults(std::vector<KeySearchResult> &results)
{
    for (size_t i = 0; i < _results.size(); i++) {
        results.push_back(_results[i]);
    }
    size_t count = _results.size();
    _results.clear();
    return count;
}

uint64_t CudaKangarooDevice::keysPerStep()
{
    // Each thread advances 8 tame + 8 wild kangaroo steps per iteration for _pointsPerThread steps
    return (uint64_t)_blocks * (uint64_t)_threads * 2ull * 8ull * (uint64_t)_pointsPerThread;
}

std::string CudaKangarooDevice::getDeviceName()
{
    return _deviceName;
}

void CudaKangarooDevice::getMemoryInfo(uint64_t &freeMem, uint64_t &totalMem)
{
    cudaCall(cudaMemGetInfo(&freeMem, &totalMem));
}

secp256k1::uint256 CudaKangarooDevice::getNextKey()
{
    // Kangaroo is not a linear sweep; keep KeyFinder from ending on keyspace
    return _startKey;
}
