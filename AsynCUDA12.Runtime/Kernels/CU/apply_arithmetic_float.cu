extern "C" __global__ void apply_arithmetic_float(float* data, int length, int op, float operand)
{
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx >= length)
	{
		return;
	}

	float v = data[idx];

	if (op == 0)
	{
		v = v + operand;
	}
	else if (op == 1)
	{
		v = v - operand;
	}
	else if (op == 2)
	{
		v = v * operand;
	}
	else if (op == 3)
	{
		v = (operand != 0.0f) ? (v / operand) : v;
	}

	data[idx] = v;
}
