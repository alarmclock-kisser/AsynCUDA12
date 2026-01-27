
		extern "C" __global__ void DoubleValues(float* data, int length)
		{
			int idx = blockIdx.x * blockDim.x + threadIdx.x;
			if (idx < length)
			{
				data[idx] = data[idx] * 2.0f;
			}
		}
		