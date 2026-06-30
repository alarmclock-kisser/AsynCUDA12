extern "C" __global__ void filter_int_equals(int* column, unsigned char* mask, int length, int value)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < length)
	{
		mask[idx] = (column[idx] == value) ? (unsigned char)1 : (unsigned char)0;
	}
}
