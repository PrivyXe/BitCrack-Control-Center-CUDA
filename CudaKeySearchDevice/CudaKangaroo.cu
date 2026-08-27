#include "CudaKangaroo.cuh"
#include "secp256k1.cuh"

__constant__ KangarooJump _KANGAROO_JUMP_TABLE[64];

// Mixed Point Addition P3(X3:Y3:Z3) = P1(X1:Y1:Z1) + P2(x2, y2) (mod P)
__device__ static void ecPointAddJacobianAffine(
    const unsigned int X1[8], const unsigned int Y1[8], const unsigned int Z1[8],
    const unsigned int x2[8], const unsigned int y2[8],
    unsigned int X3[8], unsigned int Y3[8], unsigned int Z3[8])
{
    unsigned int z1_sq[8];
    squareModP(Z1, z1_sq);               // Z1^2

    unsigned int u2[8];
    mulModP(x2, z1_sq, u2);             // U2 = x2 * Z1^2

    unsigned int z1_cubed[8];
    mulModP(Z1, z1_sq, z1_cubed);       // Z1^3

    unsigned int s2[8];
    mulModP(y2, z1_cubed, s2);          // S2 = y2 * Z1^3

    unsigned int h[8];
    subModP(u2, X1, h);                 // H = U2 - X1

    unsigned int r[8];
    subModP(s2, Y1, r);                 // R = S2 - Y1

    unsigned int h_sq[8];
    squareModP(h, h_sq);                // H^2

    unsigned int h_cubed[8];
    mulModP(h, h_sq, h_cubed);          // H^3

    unsigned int v[8];
    mulModP(X1, h_sq, v);               // V = X1 * H^2

    unsigned int r_sq[8];
    squareModP(r, r_sq);                // R^2

    unsigned int v2[8];
    addModP(v, v, v2);                  // 2*V

    unsigned int newX[8];
    subModP(r_sq, h_cubed, newX);       // R^2 - H^3
    subModP(newX, v2, newX);            // X3 = R^2 - H^3 - 2*V

    unsigned int v_sub_x3[8];
    subModP(v, newX, v_sub_x3);         // V - X3

    unsigned int r_term[8];
    mulModP(r, v_sub_x3, r_term);       // R * (V - X3)

    unsigned int y1_h_cubed[8];
    mulModP(Y1, h_cubed, y1_h_cubed);   // Y1 * H^3

    unsigned int newY[8];
    subModP(r_term, y1_h_cubed, newY);  // Y3 = R*(V - X3) - Y1*H^3

    unsigned int newZ[8];
    mulModP(Z1, h, newZ);               // Z3 = Z1 * H

    copyBigInt(newX, X3);
    copyBigInt(newY, Y3);
    copyBigInt(newZ, Z3);
}

__device__ static void kangarooStepOne(
    unsigned int x[8],
    unsigned int y[8],
    unsigned int z[8],
    unsigned int dist[8],
    unsigned int history[3],
    unsigned int jumpDistOut[8],
    unsigned int jumpXOut[8],
    unsigned int jumpYOut[8])
{
    unsigned int jIdx = x[7] % 64;
    // Bug #15 fix: stronger anti-cycle catches 1-cycles and 2-cycles (A,B,A,B...)
    if (jIdx == history[0] || (jIdx == history[1] && history[0] == history[2])) {
        jIdx = (jIdx + 7) % 64;  // +7 (prime) for better distribution
    }
    history[2] = history[1];
    history[1] = history[0];
    history[0] = jIdx;

    #pragma unroll
    for (int i = 0; i < 8; i++) {
        jumpDistOut[i] = _KANGAROO_JUMP_TABLE[jIdx].distance[i];
        jumpXOut[i] = _KANGAROO_JUMP_TABLE[jIdx].pointX[i];
        jumpYOut[i] = _KANGAROO_JUMP_TABLE[jIdx].pointY[i];
    }

    ecPointAddJacobianAffine(x, y, z, jumpXOut, jumpYOut, x, y, z);
    addModN(dist, jumpDistOut, dist);
}

__device__ static void checkDP(
    unsigned int x[8],
    unsigned int dist[8],
    unsigned int dpMask,
    DPEntry *dpBuffer,
    unsigned int *dpCount,
    unsigned int maxDpEntries,
    unsigned int kangarooType,
    unsigned int tid)
{
    if ((x[7] & dpMask) == 0) {
        unsigned int idx = atomicAdd(dpCount, 1);
        if (idx < maxDpEntries) {
            unsigned long long xKey = ((unsigned long long)x[0] << 32) | x[1];
            unsigned long long distLow = ((unsigned long long)dist[6] << 32) | dist[7];

            dpBuffer[idx].xKey = xKey;
            dpBuffer[idx].distLow = distLow;
            #pragma unroll
            for (int i = 0; i < 8; i++) {
                dpBuffer[idx].xFull[i] = x[i];
                dpBuffer[idx].distance[i] = dist[i];
            }
            dpBuffer[idx].kangarooType = kangarooType;
            dpBuffer[idx].kangarooId = tid;
        }
    }
}

// Helper for shared memory to avoid bank conflicts
__device__ __forceinline__ static void writeShared(unsigned int *s_ara, int tid, const unsigned int x[8]) {
    #pragma unroll
    for (int i = 0; i < 8; i++) {
        s_ara[i * blockDim.x + tid] = x[i];
    }
}

__device__ __forceinline__ static void readShared(const unsigned int *s_ara, int tid, unsigned int x[8]) {
    #pragma unroll
    for (int i = 0; i < 8; i++) {
        x[i] = s_ara[i * blockDim.x + tid];
    }
}

// High-Performance Parallel Batch Inversion (All threads participate)
// Uses parallel prefix scan + suffix scan in shared memory (18 parallel mulModP steps vs 512 sequential)
__device__ static void batchInvertBlockParallel(unsigned int z[8])
{
    extern __shared__ unsigned int s_mem[];
    unsigned int* s_prefix = s_mem;                                // size: blockDim.x * 8
    unsigned int* s_suffix = &s_mem[blockDim.x * 8];               // size: blockDim.x * 8

    int tid = threadIdx.x;
    int bdim = blockDim.x;

    bool isZero = (z[0] == 0 && z[1] == 0 && z[2] == 0 && z[3] == 0 &&
                   z[4] == 0 && z[5] == 0 && z[6] == 0 && z[7] == 0);

    unsigned int myZ[8];
    if (isZero) {
        #pragma unroll
        for (int i = 0; i < 7; i++) myZ[i] = 0;
        myZ[7] = 1;
    } else {
        copyBigInt(z, myZ);
    }

    // 1. Parallel Forward Inclusive Prefix Scan
    unsigned int curPrefix[8];
    copyBigInt(myZ, curPrefix);
    writeShared(s_prefix, tid, curPrefix);
    __syncthreads();

    for (int stride = 1; stride < bdim; stride *= 2) {
        if (tid >= stride) {
            unsigned int prev[8];
            readShared(s_prefix, tid - stride, prev);
            mulModP(prev, curPrefix);
        }
        __syncthreads();
        writeShared(s_prefix, tid, curPrefix);
        __syncthreads();
    }

    // 2. Parallel Backward Inclusive Suffix Scan
    unsigned int curSuffix[8];
    copyBigInt(myZ, curSuffix);
    writeShared(s_suffix, tid, curSuffix);
    __syncthreads();

    for (int stride = 1; stride < bdim; stride *= 2) {
        if (tid + stride < bdim) {
            unsigned int nextVal[8];
            readShared(s_suffix, tid + stride, nextVal);
            mulModP(nextVal, curSuffix);
        }
        __syncthreads();
        writeShared(s_suffix, tid, curSuffix);
        __syncthreads();
    }

    // 3. Invert total product (Thread bdim - 1)
    unsigned int totalInv[8];
    if (tid == bdim - 1) {
        readShared(s_prefix, bdim - 1, totalInv);
        invModP(totalInv);
        writeShared(s_prefix, bdim - 1, totalInv);
    }
    __syncthreads();

    readShared(s_prefix, bdim - 1, totalInv);

    // 4. Compute individual inverses in parallel
    unsigned int myInv[8];

    if (tid == 0) {
        if (bdim > 1) {
            unsigned int suff1[8];
            readShared(s_suffix, 1, suff1);
            mulModP(suff1, totalInv, myInv);
        } else {
            copyBigInt(totalInv, myInv);
        }
    } else if (tid == bdim - 1) {
        unsigned int prefPrev[8];
        readShared(s_prefix, bdim - 2, prefPrev);
        mulModP(prefPrev, totalInv, myInv);
    } else {
        unsigned int prefPrev[8], suffNext[8];
        readShared(s_prefix, tid - 1, prefPrev);
        readShared(s_suffix, tid + 1, suffNext);

        mulModP(prefPrev, totalInv, myInv);
        mulModP(suffNext, myInv);
    }

    if (isZero) {
        #pragma unroll
        for (int i = 0; i < 8; i++) z[i] = 0;
    } else {
        copyBigInt(myInv, z);
    }
}

__global__ void kangarooStepKernelDP(
    KangarooState *state,
    unsigned int dpMask,
    DPEntry *dpBuffer,
    unsigned int *dpCount,
    unsigned int maxDpEntries,
    unsigned int * /*foundKey*/,
    int stepsPerCall)
{
    int tid = blockIdx.x * blockDim.x + threadIdx.x;

    KangarooState s = state[tid];

    int steps = stepsPerCall <= 0 ? 1 : stepsPerCall;
    for (int step = 0; step < steps; step++) {
        
        #pragma unroll
        for (int jump = 0; jump < 8; jump++) {
            // === TAME KANGAROOS ===
            unsigned int t_jumpDist[8], t_jumpX[8], t_jumpY[8];
            kangarooStepOne(
                s.tameX, s.tameY, s.tameZ, s.tameDist, s.tameHistory,
                t_jumpDist, t_jumpX, t_jumpY);
                
            // Batch invert Tame Z (Parallel across all threads)
            batchInvertBlockParallel(s.tameZ);
            
            // Normalize Tame X, Y using inverted Z
            bool t_isZero = (s.tameZ[0] == 0 && s.tameZ[1] == 0 && s.tameZ[2] == 0 && s.tameZ[3] == 0 &&
                             s.tameZ[4] == 0 && s.tameZ[5] == 0 && s.tameZ[6] == 0 && s.tameZ[7] == 0);
            if (!t_isZero) {
                unsigned int z2[8], z3[8];
                squareModP(s.tameZ, z2);
                mulModP(s.tameZ, z2, z3);

                unsigned int affX[8], affY[8];
                mulModP(s.tameX, z2, affX);
                mulModP(s.tameY, z3, affY);

                // Copy back affine coordinates
                #pragma unroll
                for (int i = 0; i < 8; i++) {
                    s.tameX[i] = affX[i];
                    s.tameY[i] = affY[i];
                    s.tameZ[i] = 0;
                }
                s.tameZ[7] = 1;
                
                checkDP(s.tameX, s.tameDist, dpMask, dpBuffer, dpCount, maxDpEntries, 0, (unsigned int)tid);
            }

            // === WILD KANGAROOS ===
            unsigned int w_jumpDist[8], w_jumpX[8], w_jumpY[8];
            kangarooStepOne(
                s.wildX, s.wildY, s.wildZ, s.wildDist, s.wildHistory,
                w_jumpDist, w_jumpX, w_jumpY);
                
            // Batch invert Wild Z (Parallel across all threads)
            batchInvertBlockParallel(s.wildZ);
            
            // Normalize Wild X, Y using inverted Z
            bool w_isZero = (s.wildZ[0] == 0 && s.wildZ[1] == 0 && s.wildZ[2] == 0 && s.wildZ[3] == 0 &&
                             s.wildZ[4] == 0 && s.wildZ[5] == 0 && s.wildZ[6] == 0 && s.wildZ[7] == 0);
            if (!w_isZero) {
                unsigned int z2[8], z3[8];
                squareModP(s.wildZ, z2);
                mulModP(s.wildZ, z2, z3);

                unsigned int affX[8], affY[8];
                mulModP(s.wildX, z2, affX);
                mulModP(s.wildY, z3, affY);

                // Copy back affine coordinates
                #pragma unroll
                for (int i = 0; i < 8; i++) {
                    s.wildX[i] = affX[i];
                    s.wildY[i] = affY[i];
                    s.wildZ[i] = 0;
                }
                s.wildZ[7] = 1;
                
                checkDP(s.wildX, s.wildDist, dpMask, dpBuffer, dpCount, maxDpEntries, 1, (unsigned int)tid);
            }
        }
    }

    state[tid] = s;
}


cudaError_t initKangarooJumpTable(const KangarooJump *h_jumps, int numJumps)
{
    if (numJumps > 64) {
        numJumps = 64;
    }
    return cudaMemcpyToSymbol(_KANGAROO_JUMP_TABLE, h_jumps, sizeof(KangarooJump) * numJumps);
}

cudaError_t runKangarooSearchKernelDP(
    int blocks,
    int threads,
    KangarooState *d_state,
    unsigned int dpMask,
    DPEntry *d_dpBuffer,
    unsigned int *d_dpCount,
    unsigned int maxDpEntries,
    unsigned int *d_foundKey,
    int stepsPerCall)
{
    size_t sharedMemSize = 2 * threads * 8 * sizeof(unsigned int);
    kangarooStepKernelDP<<<blocks, threads, sharedMemSize>>>(d_state, dpMask, d_dpBuffer, d_dpCount, maxDpEntries, d_foundKey, stepsPerCall);
    return cudaGetLastError();
}
