#include "CudaAtomicList.h"
#include "CudaAtomicList.cuh"

#include <stdio.h>

#include <cuda.h>
#include <cuda_runtime.h>

static __constant__ void *_LIST_BUF[1];
static __constant__ unsigned int *_LIST_SIZE[1];
static __constant__ unsigned int _LIST_CAPACITY[1];


__device__ void atomicListAdd(void *info, unsigned int size)
{
	unsigned int count = atomicAdd(_LIST_SIZE[0], 1);

	if(count >= _LIST_CAPACITY[0]) {
		return;
	}

	unsigned char *ptr = (unsigned char *)(_LIST_BUF[0]) + count * size;

	memcpy(ptr, info, size);
}

static cudaError_t setListPtr(void *ptr, unsigned int *numResults, unsigned int maxItems)
{
	cudaError_t err = cudaMemcpyToSymbol(_LIST_BUF, &ptr, sizeof(void *));

	if(err) {
		return err;
	}

	err = cudaMemcpyToSymbol(_LIST_SIZE, &numResults, sizeof(unsigned int *));
	if(err) {
		return err;
	}

	return cudaMemcpyToSymbol(_LIST_CAPACITY, &maxItems, sizeof(unsigned int));
}


cudaError_t CudaAtomicList::init(unsigned int itemSize, unsigned int maxItems)
{
	cleanup();

	_itemSize = itemSize;
	_maxSize = maxItems;

	// The number of results found in the most recent kernel run
	_countHostPtr = NULL;
	cudaError_t err = cudaHostAlloc(&_countHostPtr, sizeof(unsigned int), cudaHostAllocMapped);
	if(err) {
		goto end;
	}

	// Number of items in the list
	_countDevPtr = NULL;
	err = cudaHostGetDevicePointer(&_countDevPtr, _countHostPtr, 0);
	if(err) {
		goto end;
	}
	*_countHostPtr = 0;

	// Storage for results data
	_hostPtr = NULL;
	err = cudaHostAlloc(&_hostPtr, (size_t)itemSize * maxItems, cudaHostAllocMapped);
	if(err) {
		goto end;
	}

	// Storage for results data (device to host pointer)
	_devPtr = NULL;
	err = cudaHostGetDevicePointer(&_devPtr, _hostPtr, 0);

	if(err) {
		goto end;
	}

	err = setListPtr(_devPtr, _countDevPtr, maxItems);

end:
	if(err) {
		cleanup();
	}

	return err;
}

unsigned int CudaAtomicList::size()
{
	if(_countHostPtr == NULL) return 0;
	unsigned int s = *_countHostPtr;
	return s > _maxSize ? _maxSize : s;
}

void CudaAtomicList::clear()
{
	if(_countHostPtr != NULL) {
		*_countHostPtr = 0;
	}
}

unsigned int CudaAtomicList::read(void *ptr, unsigned int count)
{
	if(_hostPtr == NULL || _countHostPtr == NULL) {
		return 0;
	}

	unsigned int actual = *_countHostPtr;
	if(actual > _maxSize) {
		actual = _maxSize;
	}

	if(count > actual) {
		count = actual;
	}

	if(count > 0 && ptr != NULL) {
		memcpy(ptr, _hostPtr, (size_t)count * _itemSize);
	}

	return count;
}

void CudaAtomicList::cleanup()
{
	if(_countHostPtr != NULL) {
		cudaFreeHost(_countHostPtr);
		_countHostPtr = NULL;
		_countDevPtr = NULL;
	}

	if(_hostPtr != NULL) {
		cudaFreeHost(_hostPtr);
		_hostPtr = NULL;
		_devPtr = NULL;
	}

	_maxSize = 0;
	_itemSize = 0;
}