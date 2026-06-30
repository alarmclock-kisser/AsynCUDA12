__device__ void atomicMinFloat(float* address, float value)
{
	int* intAddress = (int*)address;
	int old = *intAddress;
	while (value < __int_as_float(old))
	{
		int assumed = old;
		old = atomicCAS(intAddress, assumed, __float_as_int(value));
		if (old == assumed)
		{
			break;
		}
	}
}

__device__ void atomicMaxFloat(float* address, float value)
{
	int* intAddress = (int*)address;
	int old = *intAddress;
	while (value > __int_as_float(old))
	{
		int assumed = old;
		old = atomicCAS(intAddress, assumed, __float_as_int(value));
		if (old == assumed)
		{
			break;
		}
	}
}

extern "C" __global__ void min_max_float(float* values, float* outMin, float* outMax, int length)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < length)
	{
		float v = values[idx];
		atomicMinFloat(outMin, v);
		atomicMaxFloat(outMax, v);
	}
}
