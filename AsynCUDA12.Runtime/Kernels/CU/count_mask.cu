extern "C" __global__ void count_mask(unsigned char* mask, int* result, int length)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < length && mask[idx] != 0)
	{
		atomicAdd(result, 1);
	}
}
