extern "C" __global__ void string_contains(unsigned char* data, int* offsets, int* lengths, unsigned char* pattern, unsigned char* mask, int rowCount, int patternLength)
{
	int row = blockIdx.x * blockDim.x + threadIdx.x;
	if (row >= rowCount)
	{
		return;
	}

	int start = offsets[row];
	int len = lengths[row];
	unsigned char found = 0;

	if (patternLength > 0 && len >= patternLength)
	{
		for (int i = 0; i <= len - patternLength; i++)
		{
			int j = 0;
			while (j < patternLength && data[start + i + j] == pattern[j])
			{
				j++;
			}
			if (j == patternLength)
			{
				found = 1;
				break;
			}
		}
	}

	mask[row] = found;
}
