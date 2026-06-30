extern "C" __global__ void project_by_mask(float* input, unsigned char* mask, float* output, int length)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < length)
	{
		output[idx] = (mask[idx] != 0) ? input[idx] : 0.0f;
	}
}
