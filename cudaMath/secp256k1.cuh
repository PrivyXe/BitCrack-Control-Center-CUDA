#ifndef _SECP256K1_CUH
#define _SECP256K1_CUH

#include <cuda.h>
#include <cuda_runtime.h>

#include "ptx.cuh"


/**
 Prime modulus 2^256 - 2^32 - 977
 */
__constant__ static unsigned int _P[8] = {
	0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFE, 0xFFFFFC2F
};

/**
 Base point X
 */
__constant__ static unsigned int _GX[8] = {
	0x79BE667E, 0xF9DCBBAC, 0x55A06295, 0xCE870B07, 0x029BFCDB, 0x2DCE28D9, 0x59F2815B, 0x16F81798
};


/**
 Base point Y
 */
__constant__ static unsigned int _GY[8] = {
	0x483ADA77, 0x26A3C465, 0x5DA4FBFC, 0x0E1108A8, 0xFD17B448, 0xA6855419, 0x9C47D08F, 0xFB10D4B8
};


/**
 * Group order
 */
__constant__ static unsigned int _N[8] = {
	0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFE, 0xBAAEDCE6, 0xAF48A03B, 0xBFD25E8C, 0xD0364141
};

__constant__ static unsigned int _BETA[8] = {
	0x7AE96A2B, 0x657C0710, 0x6E64479E, 0xAC3434E9, 0x9CF04975, 0x12F58995, 0xC1396C28, 0x719501EE
};


__constant__ static unsigned int _LAMBDA[8] = {
	0x5363AD4C, 0xC05C30E0, 0xA5261C02, 0x8812645A, 0x122E22EA, 0x20816678, 0xDF02967C, 0x1B23BD72
};


__device__ __forceinline__ bool isInfinity(const unsigned int x[8])
{
	bool isf = true;

	for(int i = 0; i < 8; i++) {
		if(x[i] != 0xffffffff) {
			isf = false;
		}
	}

	return isf;
}

__device__ __forceinline__ static void copyBigInt(const unsigned int src[8], unsigned int dest[8])
{
	for(int i = 0; i < 8; i++) {
		dest[i] = src[i];
	}
}

__device__ static bool equal(const unsigned int *a, const unsigned int *b)
{
	bool eq = true;

	for(int i = 0; i < 8; i++) {
		eq &= (a[i] == b[i]);
	}

	return eq;
}

/**
 * Reads an 8-word big integer from device memory
 */
__device__ static void readInt(const unsigned int *ara, int idx, unsigned int x[8])
{
	int totalThreads = gridDim.x * blockDim.x;

	int base = idx * totalThreads * 8;

	int threadId = blockDim.x * blockIdx.x + threadIdx.x;

	int index = base + threadId;

	for (int i = 0; i < 8; i++) {
		x[i] = ara[index];
		index += totalThreads;
	}
}

__device__ static unsigned int readIntLSW(const unsigned int *ara, int idx)
{
	int totalThreads = gridDim.x * blockDim.x;

	int base = idx * totalThreads * 8;

	int threadId = blockDim.x * blockIdx.x + threadIdx.x;

	int index = base + threadId;

	return ara[index + totalThreads * 7];
}

/**
 * Writes an 8-word big integer to device memory
 */
__device__ static void writeInt(unsigned int *ara, int idx, const unsigned int x[8])
{
	int totalThreads = gridDim.x * blockDim.x;

	int base = idx * totalThreads * 8;

	int threadId = blockDim.x * blockIdx.x + threadIdx.x;

	int index = base + threadId;

	for (int i = 0; i < 8; i++) {
		ara[index] = x[i];
		index += totalThreads;
	}
}

/**
 * Subtraction mod p
 */
__device__ static void subModP(const unsigned int a[8], const unsigned int b[8], unsigned int c[8])
{
	sub_cc(c[7], a[7], b[7]);
	subc_cc(c[6], a[6], b[6]);
	subc_cc(c[5], a[5], b[5]);
	subc_cc(c[4], a[4], b[4]);
	subc_cc(c[3], a[3], b[3]);
	subc_cc(c[2], a[2], b[2]);
	subc_cc(c[1], a[1], b[1]);
	subc_cc(c[0], a[0], b[0]);

	unsigned int borrow = 0;
	subc(borrow, 0, 0);

	if (borrow) {
		add_cc(c[7], c[7], _P[7]);
		addc_cc(c[6], c[6], _P[6]);
		addc_cc(c[5], c[5], _P[5]);
		addc_cc(c[4], c[4], _P[4]);
		addc_cc(c[3], c[3], _P[3]);
		addc_cc(c[2], c[2], _P[2]);
		addc_cc(c[1], c[1], _P[1]);
		addc(c[0], c[0], _P[0]);
	}
}

__device__ static unsigned int add(const unsigned int a[8], const unsigned int b[8], unsigned int c[8])
{
	add_cc(c[7], a[7], b[7]);
	addc_cc(c[6], a[6], b[6]);
	addc_cc(c[5], a[5], b[5]);
	addc_cc(c[4], a[4], b[4]);
	addc_cc(c[3], a[3], b[3]);
	addc_cc(c[2], a[2], b[2]);
	addc_cc(c[1], a[1], b[1]);
	addc_cc(c[0], a[0], b[0]);

	unsigned int carry = 0;
	addc(carry, 0, 0);

	return carry;
}

__device__ static unsigned int sub(const unsigned int a[8], const unsigned int b[8], unsigned int c[8])
{
	sub_cc(c[7], a[7], b[7]);
	subc_cc(c[6], a[6], b[6]);
	subc_cc(c[5], a[5], b[5]);
	subc_cc(c[4], a[4], b[4]);
	subc_cc(c[3], a[3], b[3]);
	subc_cc(c[2], a[2], b[2]);
	subc_cc(c[1], a[1], b[1]);
	subc_cc(c[0], a[0], b[0]);

	unsigned int borrow = 0;
	subc(borrow, 0, 0);

	return (borrow & 0x01);
}


__device__ static void addModP(const unsigned int a[8], const unsigned int b[8], unsigned int c[8])
{
	unsigned int t[8];
	add_cc(t[7], a[7], b[7]);
	addc_cc(t[6], a[6], b[6]);
	addc_cc(t[5], a[5], b[5]);
	addc_cc(t[4], a[4], b[4]);
	addc_cc(t[3], a[3], b[3]);
	addc_cc(t[2], a[2], b[2]);
	addc_cc(t[1], a[1], b[1]);
	addc_cc(t[0], a[0], b[0]);

	unsigned int carry = 0;
	addc(carry, 0, 0);

	unsigned int s[8];
	sub_cc(s[7], t[7], _P[7]);
	subc_cc(s[6], t[6], _P[6]);
	subc_cc(s[5], t[5], _P[5]);
	subc_cc(s[4], t[4], _P[4]);
	subc_cc(s[3], t[3], _P[3]);
	subc_cc(s[2], t[2], _P[2]);
	subc_cc(s[1], t[1], _P[1]);
	subc_cc(s[0], t[0], _P[0]);

	unsigned int borrow = 0;
	subc(borrow, 0, 0);

	unsigned int mask = (carry | (borrow ^ 1)) ? 0xffffffff : 0;
	#pragma unroll
	for (int i = 0; i < 8; i++) {
		c[i] = (s[i] & mask) | (t[i] & ~mask);
	}
}

__device__ static void addModN(const unsigned int a[8], const unsigned int b[8], unsigned int c[8])
{
	add_cc(c[7], a[7], b[7]);
	addc_cc(c[6], a[6], b[6]);
	addc_cc(c[5], a[5], b[5]);
	addc_cc(c[4], a[4], b[4]);
	addc_cc(c[3], a[3], b[3]);
	addc_cc(c[2], a[2], b[2]);
	addc_cc(c[1], a[1], b[1]);
	addc_cc(c[0], a[0], b[0]);

	unsigned int carry = 0;
	addc(carry, 0, 0);

	bool gt = false;
	for(int i = 0; i < 8; i++) {
		if(c[i] > _N[i]) {
			gt = true;
			break;
		} else if(c[i] < _N[i]) {
			break;
		}
	}

	if(carry || gt) {
		sub_cc(c[7], c[7], _N[7]);
		subc_cc(c[6], c[6], _N[6]);
		subc_cc(c[5], c[5], _N[5]);
		subc_cc(c[4], c[4], _N[4]);
		subc_cc(c[3], c[3], _N[3]);
		subc_cc(c[2], c[2], _N[2]);
		subc_cc(c[1], c[1], _N[1]);
		subc(c[0], c[0], _N[0]);
	}
}

__device__ static void subModN(const unsigned int a[8], const unsigned int b[8], unsigned int c[8])
{
	sub_cc(c[7], a[7], b[7]);
	subc_cc(c[6], a[6], b[6]);
	subc_cc(c[5], a[5], b[5]);
	subc_cc(c[4], a[4], b[4]);
	subc_cc(c[3], a[3], b[3]);
	subc_cc(c[2], a[2], b[2]);
	subc_cc(c[1], a[1], b[1]);
	subc_cc(c[0], a[0], b[0]);

	unsigned int borrow = 0;
	subc(borrow, 0, 0);

	if (borrow) {
		add_cc(c[7], c[7], _N[7]);
		addc_cc(c[6], c[6], _N[6]);
		addc_cc(c[5], c[5], _N[5]);
		addc_cc(c[4], c[4], _N[4]);
		addc_cc(c[3], c[3], _N[3]);
		addc_cc(c[2], c[2], _N[2]);
		addc_cc(c[1], c[1], _N[1]);
		addc(c[0], c[0], _N[0]);
	}
}

__device__ static void negModN(const unsigned int value[8], unsigned int negative[8])
{
	sub_cc(negative[7], _N[7], value[7]);
	subc_cc(negative[6], _N[6], value[6]);
	subc_cc(negative[5], _N[5], value[5]);
	subc_cc(negative[4], _N[4], value[4]);
	subc_cc(negative[3], _N[3], value[3]);
	subc_cc(negative[2], _N[2], value[2]);
	subc_cc(negative[1], _N[1], value[1]);
	subc(negative[0], _N[0], value[0]);
}



__device__ static void mulModP(const unsigned int a[8], const unsigned int b[8], unsigned int c[8])
{
	unsigned int high[8] = { 0 };

	unsigned int t = a[7];

	// a[7] * b (low)
	for(int i = 7; i >= 0; i--) {
		c[i] = t * b[i];
	}

	// a[7] * b (high)
	mad_hi_cc(c[6], t, b[7], c[6]);
	madc_hi_cc(c[5], t, b[6], c[5]);
	madc_hi_cc(c[4], t, b[5], c[4]);
	madc_hi_cc(c[3], t, b[4], c[3]);
	madc_hi_cc(c[2], t, b[3], c[2]);
	madc_hi_cc(c[1], t, b[2], c[1]);
	madc_hi_cc(c[0], t, b[1], c[0]);
	madc_hi(high[7], t, b[0], high[7]);



	// a[6] * b (low)
	t = a[6];
	mad_lo_cc(c[6], t, b[7], c[6]);
	madc_lo_cc(c[5], t, b[6], c[5]);
	madc_lo_cc(c[4], t, b[5], c[4]);
	madc_lo_cc(c[3], t, b[4], c[3]);
	madc_lo_cc(c[2], t, b[3], c[2]);
	madc_lo_cc(c[1], t, b[2], c[1]);
	madc_lo_cc(c[0], t, b[1], c[0]);
	madc_lo_cc(high[7], t, b[0], high[7]);
	addc(high[6], high[6], 0);

	// a[6] * b (high)
	mad_hi_cc(c[5], t, b[7], c[5]);
	madc_hi_cc(c[4], t, b[6], c[4]);
	madc_hi_cc(c[3], t, b[5], c[3]);
	madc_hi_cc(c[2], t, b[4], c[2]);
	madc_hi_cc(c[1], t, b[3], c[1]);
	madc_hi_cc(c[0], t, b[2], c[0]);
	madc_hi_cc(high[7], t, b[1], high[7]);
	madc_hi(high[6], t, b[0], high[6]);

	// a[5] * b (low)
	t = a[5];
	mad_lo_cc(c[5], t, b[7], c[5]);
	madc_lo_cc(c[4], t, b[6], c[4]);
	madc_lo_cc(c[3], t, b[5], c[3]);
	madc_lo_cc(c[2], t, b[4], c[2]);
	madc_lo_cc(c[1], t, b[3], c[1]);
	madc_lo_cc(c[0], t, b[2], c[0]);
	madc_lo_cc(high[7], t, b[1], high[7]);
	madc_lo_cc(high[6], t, b[0], high[6]);
	addc(high[5], high[5], 0);

	// a[5] * b (high)
	mad_hi_cc(c[4], t, b[7], c[4]);
	madc_hi_cc(c[3], t, b[6], c[3]);
	madc_hi_cc(c[2], t, b[5], c[2]);
	madc_hi_cc(c[1], t, b[4], c[1]);
	madc_hi_cc(c[0], t, b[3], c[0]);
	madc_hi_cc(high[7], t, b[2], high[7]);
	madc_hi_cc(high[6], t, b[1], high[6]);
	madc_hi(high[5], t, b[0], high[5]);



	// a[4] * b (low)
	t = a[4];
	mad_lo_cc(c[4], t, b[7], c[4]);
	madc_lo_cc(c[3], t, b[6], c[3]);
	madc_lo_cc(c[2], t, b[5], c[2]);
	madc_lo_cc(c[1], t, b[4], c[1]);
	madc_lo_cc(c[0], t, b[3], c[0]);
	madc_lo_cc(high[7], t, b[2], high[7]);
	madc_lo_cc(high[6], t, b[1], high[6]);
	madc_lo_cc(high[5], t, b[0], high[5]);
	addc(high[4], high[4], 0);

	// a[4] * b (high)
	mad_hi_cc(c[3], t, b[7], c[3]);
	madc_hi_cc(c[2], t, b[6], c[2]);
	madc_hi_cc(c[1], t, b[5], c[1]);
	madc_hi_cc(c[0], t, b[4], c[0]);
	madc_hi_cc(high[7], t, b[3], high[7]);
	madc_hi_cc(high[6], t, b[2], high[6]);
	madc_hi_cc(high[5], t, b[1], high[5]);
	madc_hi(high[4], t, b[0], high[4]);



	// a[3] * b (low)
	t = a[3];
	mad_lo_cc(c[3], t, b[7], c[3]);
	madc_lo_cc(c[2], t, b[6], c[2]);
	madc_lo_cc(c[1], t, b[5], c[1]);
	madc_lo_cc(c[0], t, b[4], c[0]);
	madc_lo_cc(high[7], t, b[3], high[7]);
	madc_lo_cc(high[6], t, b[2], high[6]);
	madc_lo_cc(high[5], t, b[1], high[5]);
	madc_lo_cc(high[4], t, b[0], high[4]);
	addc(high[3], high[3], 0);

	// a[3] * b (high)
	mad_hi_cc(c[2], t, b[7], c[2]);
	madc_hi_cc(c[1], t, b[6], c[1]);
	madc_hi_cc(c[0], t, b[5], c[0]);
	madc_hi_cc(high[7], t, b[4], high[7]);
	madc_hi_cc(high[6], t, b[3], high[6]);
	madc_hi_cc(high[5], t, b[2], high[5]);
	madc_hi_cc(high[4], t, b[1], high[4]);
	madc_hi(high[3], t, b[0], high[3]);



	// a[2] * b (low)
	t = a[2];
	mad_lo_cc(c[2], t, b[7], c[2]);
	madc_lo_cc(c[1], t, b[6], c[1]);
	madc_lo_cc(c[0], t, b[5], c[0]);
	madc_lo_cc(high[7], t, b[4], high[7]);
	madc_lo_cc(high[6], t, b[3], high[6]);
	madc_lo_cc(high[5], t, b[2], high[5]);
	madc_lo_cc(high[4], t, b[1], high[4]);
	madc_lo_cc(high[3], t, b[0], high[3]);
	addc(high[2], high[2], 0);

	// a[2] * b (high)
	mad_hi_cc(c[1], t, b[7], c[1]);
	madc_hi_cc(c[0], t, b[6], c[0]);
	madc_hi_cc(high[7], t, b[5], high[7]);
	madc_hi_cc(high[6], t, b[4], high[6]);
	madc_hi_cc(high[5], t, b[3], high[5]);
	madc_hi_cc(high[4], t, b[2], high[4]);
	madc_hi_cc(high[3], t, b[1], high[3]);
	madc_hi(high[2], t, b[0], high[2]);



	// a[1] * b (low)
	t = a[1];
	mad_lo_cc(c[1], t, b[7], c[1]);
	madc_lo_cc(c[0], t, b[6], c[0]);
	madc_lo_cc(high[7], t, b[5], high[7]);
	madc_lo_cc(high[6], t, b[4], high[6]);
	madc_lo_cc(high[5], t, b[3], high[5]);
	madc_lo_cc(high[4], t, b[2], high[4]);
	madc_lo_cc(high[3], t, b[1], high[3]);
	madc_lo_cc(high[2], t, b[0], high[2]);
	addc(high[1], high[1], 0);

	// a[1] * b (high)
	mad_hi_cc(c[0], t, b[7], c[0]);
	madc_hi_cc(high[7], t, b[6], high[7]);
	madc_hi_cc(high[6], t, b[5], high[6]);
	madc_hi_cc(high[5], t, b[4], high[5]);
	madc_hi_cc(high[4], t, b[3], high[4]);
	madc_hi_cc(high[3], t, b[2], high[3]);
	madc_hi_cc(high[2], t, b[1], high[2]);
	madc_hi(high[1], t, b[0], high[1]);



	// a[0] * b (low)
	t = a[0];
	mad_lo_cc(c[0], t, b[7], c[0]);
	madc_lo_cc(high[7], t, b[6], high[7]);
	madc_lo_cc(high[6], t, b[5], high[6]);
	madc_lo_cc(high[5], t, b[4], high[5]);
	madc_lo_cc(high[4], t, b[3], high[4]);
	madc_lo_cc(high[3], t, b[2], high[3]);
	madc_lo_cc(high[2], t, b[1], high[2]);
	madc_lo_cc(high[1], t, b[0], high[1]);
	addc(high[0], high[0], 0);

	// a[0] * b (high)
	mad_hi_cc(high[7], t, b[7], high[7]);
	madc_hi_cc(high[6], t, b[6], high[6]);
	madc_hi_cc(high[5], t, b[5], high[5]);
	madc_hi_cc(high[4], t, b[4], high[4]);
	madc_hi_cc(high[3], t, b[3], high[3]);
	madc_hi_cc(high[2], t, b[2], high[2]);
	madc_hi_cc(high[1], t, b[1], high[1]);
	madc_hi(high[0], t, b[0], high[0]);



	// At this point we have 16 32-bit words representing a 512-bit value
	// high[0 ... 7] and c[0 ... 7]
	const unsigned int s = 977;

	// Store high[6] and high[7] since they will be overwritten
	unsigned int high7 = high[7];
	unsigned int high6 = high[6];


	// Take high 256 bits, multiply by 2^32, add to low 256 bits
	// That is, take high[0 ... 7], shift it left 1 word and add it to c[0 ... 7]
	add_cc(c[6], high[7], c[6]);
	addc_cc(c[5], high[6], c[5]);
	addc_cc(c[4], high[5], c[4]);
	addc_cc(c[3], high[4], c[3]);
	addc_cc(c[2], high[3], c[2]);
	addc_cc(c[1], high[2], c[1]);
	addc_cc(c[0], high[1], c[0]);
	addc_cc(high[7], high[0], 0);
	addc(high[6], 0, 0);


	// Take high 256 bits, multiply by 977, add to low 256 bits
	// That is, take high[0 ... 5], high6, high7, multiply by 977 and add to c[0 ... 7]
	mad_lo_cc(c[7], high7, s, c[7]);
	madc_lo_cc(c[6], high6, s, c[6]);
	madc_lo_cc(c[5], high[5], s, c[5]);
	madc_lo_cc(c[4], high[4], s, c[4]);
	madc_lo_cc(c[3], high[3], s, c[3]);
	madc_lo_cc(c[2], high[2], s, c[2]);
	madc_lo_cc(c[1], high[1], s, c[1]);
	madc_lo_cc(c[0], high[0], s, c[0]);
	addc_cc(high[7], high[7], 0);
	addc(high[6], high[6], 0);


	mad_hi_cc(c[6], high7, s, c[6]);
	madc_hi_cc(c[5], high6, s, c[5]);
	madc_hi_cc(c[4], high[5], s, c[4]);
	madc_hi_cc(c[3], high[4], s, c[3]);
	madc_hi_cc(c[2], high[3], s, c[2]);
	madc_hi_cc(c[1], high[2], s, c[1]);
	madc_hi_cc(c[0], high[1], s, c[0]);
	madc_hi_cc(high[7], high[0], s, high[7]);
	addc(high[6], high[6], 0);


	// Repeat the same steps, but this time we only need to handle high[6] and high[7]
	high7 = high[7];
	high6 = high[6];

	// Take the high 64 bits, multiply by 2^32 and add to the low 256 bits
	add_cc(c[6], high[7], c[6]);
	addc_cc(c[5], high[6], c[5]);
	addc_cc(c[4], c[4], 0);
	addc_cc(c[3], c[3], 0);
	addc_cc(c[2], c[2], 0);
	addc_cc(c[1], c[1], 0);
	addc_cc(c[0], c[0], 0);
	addc(high[7], 0, 0);


	// Take the high 64 bits, multiply by 977 and add to the low 256 bits
	mad_lo_cc(c[7], high7, s, c[7]);
	madc_lo_cc(c[6], high6, s, c[6]);
	addc_cc(c[5], c[5], 0);
	addc_cc(c[4], c[4], 0);
	addc_cc(c[3], c[3], 0);
	addc_cc(c[2], c[2], 0);
	addc_cc(c[1], c[1], 0);
	addc_cc(c[0], c[0], 0);
	addc(high[7], high[7], 0);

	mad_hi_cc(c[6], high7, s, c[6]);
	madc_hi_cc(c[5], high6, s, c[5]);
	addc_cc(c[4], c[4], 0);
	addc_cc(c[3], c[3], 0);
	addc_cc(c[2], c[2], 0);
	addc_cc(c[1], c[1], 0);
	addc_cc(c[0], c[0], 0);
	addc(high[7], high[7], 0);


	bool overflow = high[7] != 0;

	unsigned int borrow = sub(c, _P, c);

	if(overflow) {
		if(!borrow) {
			sub(c, _P, c);
		}
	} else {
		if(borrow) {
			add(c, _P, c);
		}
	}
}


/**
 * Square mod P
 * b = a * a
 */
__device__ static void squareModP(const unsigned int a[8], unsigned int b[8])
{
	mulModP(a, a, b);
}

/**
 * Square mod P
 * x = x * x
 */
__device__ static void squareModP(unsigned int x[8])
{
	unsigned int tmp[8];
	squareModP(x, tmp);
	copyBigInt(tmp, x);
}

/**
 * Multiply mod P
 * c = a * c
 */
__device__ static void mulModP(const unsigned int a[8], unsigned int c[8])
{
	unsigned int tmp[8];
	mulModP(a, c, tmp);

	copyBigInt(tmp, c);
}

/**
 * Multiplicative inverse mod P using Fermat's method of x^(p-2) mod p and addition chains
 */
__device__ static void invModP(unsigned int value[8])
{
	unsigned int x[8];

	copyBigInt(value, x);

	unsigned int y[8] = { 0, 0, 0, 0, 0, 0, 0, 1 };

	// 0xd - 1101
	mulModP(x, y);
	squareModP(x);
	//mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);


	// 0x2 - 0010
	//mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);
	//mulModP(x, y);
	squareModP(x);
	//mulModP(x, y);
	squareModP(x);

	// 0xc = 0x1100
	//mulModP(x, y);
	squareModP(x);
	//mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);

	// 0xfffff
	for(int i = 0; i < 20; i++) {
		mulModP(x, y);
		squareModP(x);
	}

	// 0xe - 1110
	//mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);
	mulModP(x, y);
	squareModP(x);

	// 0xfffffffffffffffffffffffffffffffffffffffffffffffffffffff
	for(int i = 0; i < 219; i++) {
		mulModP(x, y);
		squareModP(x);
	}
	mulModP(x, y);

	copyBigInt(y, value);
}

__device__ static void invModP(const unsigned int *value, unsigned int *inverse)
{
	copyBigInt(value, inverse);

	invModP(inverse);
}

__device__ static void negModP(const unsigned int *value, unsigned int *negative)
{
	// Bug #4 fix: borrow chain must go LSW→MSW ([7]→[0]), consistent with subModP
	sub_cc(negative[7], _P[7], value[7]);
	subc_cc(negative[6], _P[6], value[6]);
	subc_cc(negative[5], _P[5], value[5]);
	subc_cc(negative[4], _P[4], value[4]);
	subc_cc(negative[3], _P[3], value[3]);
	subc_cc(negative[2], _P[2], value[2]);
	subc_cc(negative[1], _P[1], value[1]);
	subc(negative[0], _P[0], value[0]);
}


__device__ __forceinline__ static void beginBatchAdd(const unsigned int *px, const unsigned int *x, unsigned int *chain, int i, int batchIdx, unsigned int inverse[8])
{
	// x = Gx - x
	unsigned int t[8];
	subModP(px, x, t);

	// Keep a chain of multiples of the diff, i.e. c[0] = diff0, c[1] = diff0 * diff1,
	// c[2] = diff2 * diff1 * diff0, etc
	mulModP(t, inverse);

	writeInt(chain, batchIdx, inverse);
}


__device__ __forceinline__ static void beginBatchAddWithDouble(const unsigned int *px, const unsigned int *py, unsigned int *xPtr, unsigned int *chain, int i, int batchIdx, unsigned int inverse[8])
{
	unsigned int x[8];
	readInt(xPtr, i, x);

	if(equal(px, x)) {
		addModP(py, py, x);
	} else {
		// x = Gx - x
		subModP(px, x, x);
	}

	// Keep a chain of multiples of the diff, i.e. c[0] = diff0, c[1] = diff0 * diff1,
	// c[2] = diff2 * diff1 * diff0, etc
	mulModP(x, inverse);

	writeInt(chain, batchIdx, inverse);
}

__device__ static void completeBatchAddWithDouble(const unsigned int *px, const unsigned int *py, const unsigned int *xPtr, const unsigned int *yPtr, int i, int batchIdx, unsigned int *chain, unsigned int *inverse, unsigned int newX[8], unsigned int newY[8])
{
	unsigned int s[8];
	unsigned int x[8];
	unsigned int y[8];

	readInt(xPtr, i, x);
	readInt(yPtr, i, y);

	if(batchIdx >= 1) {
		unsigned int c[8];

		readInt(chain, batchIdx - 1, c);

		mulModP(inverse, c, s);

		unsigned int diff[8];
		if(equal(px, x)) {
			addModP(py, py, diff);
		} else {
			subModP(px, x, diff);
		}

		mulModP(diff, inverse);
	} else {
		copyBigInt(inverse, s);
	}


	if(equal(px, x)) {
		// currently s = 1 / 2y

		unsigned int x2[8];
		unsigned int tx2[8];

		// 3x^2
		mulModP(x, x, x2);
		addModP(x2, x2, tx2);
		addModP(x2, tx2, tx2);


		// s = 3x^2 * 1/2y
		mulModP(tx2, s);

		// s^2
		unsigned int s2[8];
		mulModP(s, s, s2);

		// Rx = s^2 - 2px
		subModP(s2, x, newX);
		subModP(newX, x, newX);

		// Ry = s(px - rx) - py
		unsigned int k[8];
		subModP(px, newX, k);
		mulModP(s, k, newY);
		subModP(newY, py, newY);

	} else {

		unsigned int rise[8];
		subModP(py, y, rise);

		mulModP(rise, s);

		// Rx = s^2 - Gx - Qx
		unsigned int s2[8];
		mulModP(s, s, s2);

		subModP(s2, px, newX);
		subModP(newX, x, newX);

		// Ry = s(px - rx) - py
		unsigned int k[8];
		subModP(px, newX, k);
		mulModP(s, k, newY);
		subModP(newY, py, newY);
	}
}

__device__ static void completeBatchAdd(const unsigned int *px, const unsigned int *py, unsigned int *xPtr, unsigned int *yPtr, int i, int batchIdx, unsigned int *chain, unsigned int *inverse, unsigned int newX[8], unsigned int newY[8])
{
	unsigned int s[8];
	unsigned int x[8];

	readInt(xPtr, i, x);

	if(batchIdx >= 1) {
		unsigned int c[8];

		readInt(chain, batchIdx - 1, c);
		mulModP(inverse, c, s);

		unsigned int diff[8];
		subModP(px, x, diff);
		mulModP(diff, inverse);
	} else {
		copyBigInt(inverse, s);
	}

	unsigned int y[8];
	readInt(yPtr, i, y);

	unsigned int rise[8];
	subModP(py, y, rise);

	mulModP(rise, s);

	// Rx = s^2 - Gx - Qx
	unsigned int s2[8];
	mulModP(s, s, s2);
	subModP(s2, px, newX);
	subModP(newX, x, newX);

	// Ry = s(px - rx) - py
	unsigned int k[8];
	subModP(px, newX, k);
	mulModP(s, k, newY);
	subModP(newY, py, newY);
}


__device__ __forceinline__ static void doBatchInverse(unsigned int inverse[8])
{
	__shared__ unsigned int s_mem[256 * 16];
	unsigned int* s_prefix = s_mem;
	unsigned int* s_suffix = &s_mem[blockDim.x * 8];

	int tid = threadIdx.x;
	int warpLane = tid % 32;
	int warpId = tid / 32;
	int warpBase = warpId * 32;

	bool isZero = (inverse[0] == 0 && inverse[1] == 0 && inverse[2] == 0 && inverse[3] == 0 &&
	               inverse[4] == 0 && inverse[5] == 0 && inverse[6] == 0 && inverse[7] == 0);

	unsigned int myZ[8];
	if (isZero) {
		#pragma unroll
		for (int i = 0; i < 7; i++) myZ[i] = 0;
		myZ[7] = 1;
	} else {
		copyBigInt(inverse, myZ);
	}

	// 1. Store initial values
	#pragma unroll
	for (int i = 0; i < 8; i++) {
		s_prefix[(warpBase + warpLane) * 8 + i] = myZ[i];
		s_suffix[(warpBase + warpLane) * 8 + i] = myZ[i];
	}
	__syncwarp();

	// 2. Warp-level Forward Prefix Scan
	for (int stride = 1; stride < 32; stride *= 2) {
		unsigned int cur[8];
		#pragma unroll
		for (int i = 0; i < 8; i++) cur[i] = s_prefix[(warpBase + warpLane) * 8 + i];

		if (warpLane >= stride) {
			unsigned int prev[8];
			#pragma unroll
			for (int i = 0; i < 8; i++) prev[i] = s_prefix[(warpBase + warpLane - stride) * 8 + i];
			mulModP(prev, cur);
			#pragma unroll
			for (int i = 0; i < 8; i++) s_prefix[(warpBase + warpLane) * 8 + i] = cur[i];
		}
		__syncwarp();
	}

	// 3. Warp-level Backward Suffix Scan
	for (int stride = 1; stride < 32; stride *= 2) {
		unsigned int cur[8];
		#pragma unroll
		for (int i = 0; i < 8; i++) cur[i] = s_suffix[(warpBase + warpLane) * 8 + i];

		if (warpLane + stride < 32) {
			unsigned int nextVal[8];
			#pragma unroll
			for (int i = 0; i < 8; i++) nextVal[i] = s_suffix[(warpBase + warpLane + stride) * 8 + i];
			mulModP(nextVal, cur);
			#pragma unroll
			for (int i = 0; i < 8; i++) s_suffix[(warpBase + warpLane) * 8 + i] = cur[i];
		}
		__syncwarp();
	}

	// 4. Invert total warp product (Lane 31 of each warp)
	if (warpLane == 31) {
		unsigned int totalInv[8];
		#pragma unroll
		for (int i = 0; i < 8; i++) totalInv[i] = s_prefix[(warpBase + 31) * 8 + i];
		invModP(totalInv);
		#pragma unroll
		for (int i = 0; i < 8; i++) s_prefix[(warpBase + 31) * 8 + i] = totalInv[i];
	}
	__syncwarp();

	// 5. Compute individual inverses in parallel
	unsigned int totalWarpInv[8];
	#pragma unroll
	for (int i = 0; i < 8; i++) totalWarpInv[i] = s_prefix[(warpBase + 31) * 8 + i];

	unsigned int myInv[8];
	if (warpLane == 0) {
		unsigned int suff1[8];
		#pragma unroll
		for (int i = 0; i < 8; i++) suff1[i] = s_suffix[(warpBase + 1) * 8 + i];
		mulModP(suff1, totalWarpInv, myInv);
	} else if (warpLane == 31) {
		unsigned int prefPrev[8];
		#pragma unroll
		for (int i = 0; i < 8; i++) prefPrev[i] = s_prefix[(warpBase + 30) * 8 + i];
		mulModP(prefPrev, totalWarpInv, myInv);
	} else {
		unsigned int prefPrev[8], suffNext[8];
		#pragma unroll
		for (int i = 0; i < 8; i++) {
			prefPrev[i] = s_prefix[(warpBase + warpLane - 1) * 8 + i];
			suffNext[i] = s_suffix[(warpBase + warpLane + 1) * 8 + i];
		}
		mulModP(prefPrev, totalWarpInv, myInv);
		mulModP(suffNext, myInv);
	}

	if (isZero) {
		#pragma unroll
		for (int i = 0; i < 8; i++) inverse[i] = 0;
	} else {
		copyBigInt(myInv, inverse);
	}
}

#endif