#ifndef _CUDA_KANGAROO_DEVICE_H
#define _CUDA_KANGAROO_DEVICE_H

#include "KeySearchDevice.h"
#include "CudaKangaroo.cuh"
#include "KangarooCollisionDetector.h"
#include <memory>
#include <vector>

class CudaKangarooDevice : public KeySearchDevice {

private:
    int _device;
    int _blocks;
    int _threads;
    int _pointsPerThread;
    unsigned int _dpBits;
    unsigned int _dpMask;
    unsigned int _maxDpEntries;

    std::string _deviceName;

    secp256k1::uint256 _startKey;
    secp256k1::uint256 _endKey;
    secp256k1::ecpoint _targetPubKey;
    bool _hasTarget = false;

    uint64_t _iterations = 0;

    KangarooState *_d_state = nullptr;
    DPEntry *_d_dpBuffer = nullptr;
    unsigned int *_d_dpCount = nullptr;
    unsigned int *_d_foundKey = nullptr;

    std::vector<DPEntry> _h_dpBuffer;
    std::unique_ptr<KangarooCollisionDetector> _detector;
    std::vector<KeySearchResult> _results;
    std::vector<hash160> _targets;

    void cudaCall(cudaError_t err);
    void freeDevice();
    void buildJumpTable();
    void initKangaroos();
    int bitLength(const secp256k1::uint256 &value) const;

public:
    CudaKangarooDevice(int device, int threads, int blocks, unsigned int dpBits, int pointsPerThread = 32);
    virtual ~CudaKangarooDevice();

    void setTargetPublicKey(const secp256k1::ecpoint &pk);
    void setKeyRange(const secp256k1::uint256 &start, const secp256k1::uint256 &end);

    virtual void init(const secp256k1::uint256 &start, int compression, const secp256k1::uint256 &stride);
    virtual void doStep();
    virtual void setTargets(const std::set<KeySearchTarget> &targets);
    virtual size_t getResults(std::vector<KeySearchResult> &results);
    virtual uint64_t keysPerStep();
    virtual std::string getDeviceName();
    virtual void getMemoryInfo(uint64_t &freeMem, uint64_t &totalMem);
    virtual secp256k1::uint256 getNextKey();
};

#endif
