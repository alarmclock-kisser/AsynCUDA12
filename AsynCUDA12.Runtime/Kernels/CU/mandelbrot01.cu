extern "C" __global__ void mandelbrot01(
    const unsigned char* inputPixels,
    unsigned char* outputPixels,
    int width,
    int height,
    double zoom,
    double offsetX,
    double offsetY,
    int iterCoeff,
    int baseR,
    int baseG,
    int baseB)
{
    int px = blockIdx.x * blockDim.x + threadIdx.x;
    int py = blockIdx.y * blockDim.y + threadIdx.y;

    // Clamp iterCoeff to 1-1000
    iterCoeff = max(1, min(iterCoeff, 1000));

    if (px >= width || py >= height) return;

    // Double precision zoom calculation
    int maxIter = 100 + static_cast<int>(iterCoeff * log(zoom + 1.0));

    // Double precision coordinates
    double x0 = (px - width/2.0) / (width/2.0) / zoom + offsetX;
    double y0 = (py - height/2.0) / (height/2.0) / zoom + offsetY;

    // Mixed precision iteration (double for coordinates, float for speed)
    double x = 0.0;
    double y = 0.0;
    int iter = 0;

    while (x*x + y*y <= 4.0 && iter < maxIter)
    {
        double xtemp = x*x - y*y + x0;
        y = 2.0*x*y + y0;
        x = xtemp;
        iter++;
    }

    // Color calculation remains in float for performance
    int idx = (py * width + px) * 4;
    
    if (iter == maxIter)
    {
        outputPixels[idx+0] = baseR;
        outputPixels[idx+1] = baseG;
        outputPixels[idx+2] = baseB;
    }
    else
    {
        float t = (float)iter / (float)maxIter;
        float r = __sinf(t * 3.14159f) * 255.0f;
        float g = __sinf(t * 6.28318f + 1.0472f) * 255.0f;
        float b = __sinf(t * 9.42477f + 2.0944f) * 255.0f;
        
        outputPixels[idx+0] = min(255, baseR + (int)(r * (1.0f - t)));
        outputPixels[idx+1] = min(255, baseG + (int)(g * (1.0f - t)));
        outputPixels[idx+2] = min(255, baseB + (int)(b * (1.0f - t)));
    }
    outputPixels[idx+3] = 255;
}
