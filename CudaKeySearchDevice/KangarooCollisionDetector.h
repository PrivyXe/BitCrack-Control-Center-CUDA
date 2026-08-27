#ifndef _KANGAROO_COLLISION_DETECTOR_H
#define _KANGAROO_COLLISION_DETECTOR_H

#include <unordered_map>
#include <cstring>
#include "CudaKangaroo.cuh"
#include "secp256k1.h"

class KangarooCollisionDetector {
private:
    // Bug #3 fix: multimap preserves multiple entries with the same 64-bit xKey,
    // preventing data loss when different 256-bit X coordinates hash to the same key.
    std::unordered_multimap<unsigned long long, DPEntry> _dpMap;
    secp256k1::ecpoint _targetPubKey;
    secp256k1::uint256 _keyStart;
    secp256k1::uint256 _keyEnd;

public:
    KangarooCollisionDetector(
        const secp256k1::uint256 &keyStart,
        const secp256k1::uint256 &keyEnd,
        const secp256k1::ecpoint &targetPubKey)
        : _targetPubKey(targetPubKey), _keyStart(keyStart), _keyEnd(keyEnd) {}

    bool checkCandidateKey(const secp256k1::uint256 &cand, secp256k1::uint256 &outFoundKey)
    {
        if (cand.cmp(_keyStart) >= 0 && cand.cmp(_keyEnd) <= 0) {
            secp256k1::ecpoint testPoint = secp256k1::multiplyPoint(cand, secp256k1::G());
            if (testPoint.x == _targetPubKey.x && testPoint.y == _targetPubKey.y) {
                outFoundKey = cand;
                return true;
            }
        }
        return false;
    }

    bool tryCollision(const DPEntry &existing, const DPEntry &entry, secp256k1::uint256 &outFoundKey)
    {
        // Only check opposite-type kangaroos with matching full X coordinates
        if (existing.kangarooType == entry.kangarooType) return false;
        if (memcmp(existing.xFull, entry.xFull, sizeof(entry.xFull)) != 0) return false;

        const DPEntry &tameDP = (existing.kangarooType == 0) ? existing : entry;
        const DPEntry &wildDP = (existing.kangarooType == 1) ? existing : entry;

        // GPU stores limbs big-endian; host uint256 is little-endian
        secp256k1::uint256 deltaTame(tameDP.distance, secp256k1::uint256::BigEndian);
        secp256k1::uint256 deltaWild(wildDP.distance, secp256k1::uint256::BigEndian);

        // 1) Direct collision match: k = tameDist - wildDist (mod N)
        secp256k1::uint256 cand1 = secp256k1::subModN(deltaTame, deltaWild);
        if (checkCandidateKey(cand1, outFoundKey)) return true;

        // 2) GLV Endomorphism match #1: k = (tameDist * LAMBDA) - wildDist (mod N)
        secp256k1::uint256 lambda(
            "5363AD4CC05C30E0A5261C028812645A122E22EA20816678DF02967C1B23BD72"
        );
        secp256k1::uint256 tameLambda = secp256k1::multiplyModN(deltaTame, lambda);
        secp256k1::uint256 cand2 = secp256k1::subModN(tameLambda, deltaWild);
        if (checkCandidateKey(cand2, outFoundKey)) return true;

        // 3) GLV Endomorphism match #2: k = (tameDist * LAMBDA^2) - wildDist (mod N)
        secp256k1::uint256 lambda2 = secp256k1::multiplyModN(tameLambda, lambda);
        secp256k1::uint256 cand3 = secp256k1::subModN(lambda2, deltaWild);
        if (checkCandidateKey(cand3, outFoundKey)) return true;

        return false;
    }

    bool addAndCheckDP(const DPEntry &entry, secp256k1::uint256 &outFoundKey)
    {
        // Scan all existing entries with the same 64-bit xKey
        auto range = _dpMap.equal_range(entry.xKey);
        for (auto it = range.first; it != range.second; ++it) {
            if (tryCollision(it->second, entry, outFoundKey)) {
                return true;
            }
        }

        // Insert the new entry (cap per-key entries at 4 to prevent unbounded RAM growth)
        if (_dpMap.count(entry.xKey) < 4) {
            _dpMap.emplace(entry.xKey, entry);
        }

        return false;
    }

    size_t size() const
    {
        return _dpMap.size();
    }

    void clear()
    {
        _dpMap.clear();
    }
};

#endif
