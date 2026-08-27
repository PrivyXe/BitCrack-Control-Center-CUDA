#ifndef _CUDA_MATH_PTX_CUH
#define _CUDA_MATH_PTX_CUH

#include "ptx.cuh"

// 256-bit Addition using PTX Assembly carry chaining
__device__ __forceinline__ void ptx_add256(const unsigned int *a, const unsigned int *b, unsigned int *r)
{
    add_cc(r[0], a[0], b[0]);
    addc_cc(r[1], a[1], b[1]);
    addc_cc(r[2], a[2], b[2]);
    addc_cc(r[3], a[3], b[3]);
    addc_cc(r[4], a[4], b[4]);
    addc_cc(r[5], a[5], b[5]);
    addc_cc(r[6], a[6], b[6]);
    addc(r[7], a[7], b[7]);
}

// 256-bit Subtraction using PTX Assembly borrow chaining
__device__ __forceinline__ void ptx_sub256(const unsigned int *a, const unsigned int *b, unsigned int *r)
{
    sub_cc(r[0], a[0], b[0]);
    subc_cc(r[1], a[1], b[1]);
    subc_cc(r[2], a[2], b[2]);
    subc_cc(r[3], a[3], b[3]);
    subc_cc(r[4], a[4], b[4]);
    subc_cc(r[5], a[5], b[5]);
    subc_cc(r[6], a[6], b[6]);
    subc(r[7], a[7], b[7]);
}

#endif
