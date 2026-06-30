extern "C" __global__ void hash_join_int(int* probeKeys, int* tableKeys, int* tableValues, int* outRowIds, int probeLength, int tableSize)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx >= probeLength)
	{
		return;
	}

	int result = -1;

	if (tableSize > 0)
	{
		int key = probeKeys[idx];
		unsigned int hash = ((unsigned int)key * 2654435761u) % (unsigned int)tableSize;

		for (int probe = 0; probe < tableSize; probe++)
		{
			int slot = (int)((hash + (unsigned int)probe) % (unsigned int)tableSize);
			int candidate = tableKeys[slot];
			if (candidate == -1)
			{
				break;
			}
			if (candidate == key)
			{
				result = tableValues[slot];
				break;
			}
		}
	}

	outRowIds[idx] = result;
}
