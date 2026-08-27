#ifndef _CUDA_KANGAROO_CUH
#define _CUDA_KANGAROO_CUH

#include <cuda_runtime.h>
#include "secp256k1.h"

// Van Oorschot-Wiener Distinguished Point Entry Structure (~84 bytes)
struct DPEntry {
    unsigned long long xKey;    // Lower 64-bit of X coordinate for 2^-64 collision detection
    unsigned long long distLow; // Lower 64-bit of Delta distance
    unsigned int xFull[8];      // Full 256-bit X coordinate
    unsigned int distance[8];   // Full 256-bit accumulated distance (Delta_n mod N)
    unsigned int kangarooType;  // 0 = Tame, 1 = Wild
    unsigned int kangarooId;    // Thread / Kangaroo ID
};

struct KangarooJump {
    unsigned int distance[8];
    unsigned int pointX[8];
    unsigned int pointY[8];
};

struct KangarooState {
    // Tame Kangaroo State (Jacobian X, Y, Z)
    unsigned int tameX[8];
    unsigned int tameY[8];
    unsigned int tameZ[8];
    unsigned int tameDist[8];
    unsigned int tameHistory[3];

    // Wild Kangaroo State (Jacobian X, Y, Z)
    unsigned int wildX[8];
    unsigned int wildY[8];
    unsigned int wildZ[8];
    unsigned int wildDist[8];
    unsigned int wildHistory[3];
};

// Global Kangaroo CUDA API declarations
cudaError_t initKangarooJumpTable(const KangarooJump *h_jumps, int numJumps);
cudaError_t runKangarooSearchKernelDP(
    int blocks, 
    int threads, 
    KangarooState *d_state, 
    unsigned int dpMask, 
    DPEntry *d_dpBuffer, 
    unsigned int *d_dpCount, 
    unsigned int maxDpEntries,
    unsigned int *d_foundKey,
    int stepsPerCall
);

#endif
