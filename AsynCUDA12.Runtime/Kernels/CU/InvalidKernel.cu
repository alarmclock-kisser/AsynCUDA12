
			__global__ void InvalidKernel(float* data)
			{
				int idx = threadIdx.x + blockIdx.x * blockDim.x;
				data[idx] = data[idx] * 2.0f // Missing semicolon
			}
			