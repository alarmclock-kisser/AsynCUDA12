extern "C" __global__ void build_hash_index_int(int* keys, int* tableKeys, int* tableValues, int length, int tableSize)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx >= length || tableSize <= 0)
	{
		return;
	}

	int key = keys[idx];
	unsigned int hash = ((unsigned int)key * 2654435761u) % (unsigned int)tableSize;

	for (int probe = 0; probe < tableSize; probe++)
	{
		int slot = (int)((hash + (unsigned int)probe) % (unsigned int)tableSize);
		int previous = atomicCAS(&tableKeys[slot], -1, key);
		if (previous == -1 || previous == key)
		{
			tableValues[slot] = idx;
			return;
		}
	}
}
