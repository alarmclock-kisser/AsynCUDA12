extern "C" __global__ void filter_int_range(int* column, unsigned char* mask, int length, int minValue, int maxValue)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < length)
	{
		int v = column[idx];
		mask[idx] = (v >= minValue && v <= maxValue) ? (unsigned char)1 : (unsigned char)0;
	}
}
