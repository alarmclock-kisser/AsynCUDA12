extern "C" __global__ void filter_float_range(float* column, unsigned char* mask, int length, float minValue, float maxValue)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < length)
	{
		float v = column[idx];
		mask[idx] = (v >= minValue && v <= maxValue) ? (unsigned char)1 : (unsigned char)0;
	}
}
