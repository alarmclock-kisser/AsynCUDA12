extern "C" __global__ void sum_float_by_mask(float* values, unsigned char* mask, float* result, int length)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < length && mask[idx] != 0)
	{
		atomicAdd(result, values[idx]);
	}
}
