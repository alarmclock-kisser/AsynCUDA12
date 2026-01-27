
		extern "C" __global__ void InvertImage(unsigned char* image, int width, int height)
		{
			int x = blockIdx.x * blockDim.x + threadIdx.x;
			int y = blockIdx.y * blockDim.y + threadIdx.y;
			if (x < width && y < height)
			{
				int idx = (y * width + x) * 3; // Assuming 3 channels (RGB)
				image[idx] = 255 - image[idx];       // Invert Red
				image[idx + 1] = 255 - image[idx + 1]; // Invert Green
				image[idx + 2] = 255 - image[idx + 2]; // Invert Blue
			}
		}
		