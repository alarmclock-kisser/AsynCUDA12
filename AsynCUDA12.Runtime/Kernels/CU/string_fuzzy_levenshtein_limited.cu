extern "C" __global__ void string_fuzzy_levenshtein_limited(unsigned char* data, int* offsets, int* lengths, unsigned char* pattern, unsigned char* mask, int rowCount, int patternLength, int maxDistance)
{
	int row = blockIdx.x * blockDim.x + threadIdx.x;
	if (row >= rowCount)
	{
		return;
	}

	const int MAXLEN = 64;

	int plen = patternLength;
	if (plen > MAXLEN)
	{
		plen = MAXLEN;
	}

	int slen = lengths[row];
	if (slen > MAXLEN)
	{
		slen = MAXLEN;
	}

	int start = offsets[row];

	int prev[MAXLEN + 1];
	int curr[MAXLEN + 1];

	for (int j = 0; j <= plen; j++)
	{
		prev[j] = j;
	}

	for (int i = 1; i <= slen; i++)
	{
		curr[0] = i;
		unsigned char sc = data[start + i - 1];
		for (int j = 1; j <= plen; j++)
		{
			int cost = (sc == pattern[j - 1]) ? 0 : 1;
			int del = prev[j] + 1;
			int ins = curr[j - 1] + 1;
			int sub = prev[j - 1] + cost;
			int best = del < ins ? del : ins;
			best = best < sub ? best : sub;
			curr[j] = best;
		}
		for (int j = 0; j <= plen; j++)
		{
			prev[j] = curr[j];
		}
	}

	mask[row] = (prev[plen] <= maxDistance) ? (unsigned char)1 : (unsigned char)0;
}
