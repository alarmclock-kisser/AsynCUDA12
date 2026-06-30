using System;
using System.Numerics;
using AsynCUDA.OpenCl;
using AsynCUDA12.Tests.GpuDatabase;
using Shouldly;

namespace AsynCUDA12.Tests.OpenCl
{
    /// <summary>
    /// Integration tests for <see cref="OpenClFourier"/> covering both transform modes:
    /// the fixed-size <b>chunked</b> path (<see cref="OpenClFourier.FftChunked(float[], int, int)"/> /
    /// <see cref="OpenClFourier.IfftChunked(Vector2[], int, int)"/>, which normalizes on the device) and the
    /// arbitrary-length <b>full</b> path (<see cref="OpenClFourier.Fft(float[])"/> /
    /// <see cref="OpenClFourier.Ifft(Vector2[], int)"/>, which pads to the next power of two). Guarded by
    /// <see cref="TestData.RequireOpenCl"/> so the suite is skipped as inconclusive without an OpenCL runtime.
    /// </summary>
    [TestClass]
    public sealed class OpenClFourierIntegrationTest
    {
        // A small chunk keeps the single-work-item full FFT fast while still exercising the math.
        private const int ChunkSize = 1024;

        private OpenClService Service = null!;
        private OpenClFourier Fourier = null!;


        [TestInitialize]
        public void Initialize()
        {
            this.Service = TestData.CreateOnlineOpenClServiceOrSkip()!;
            this.Fourier = this.Service.Fourier!;
            this.Fourier.ShouldNotBeNull();
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.Service?.Dispose();
        }



        // Helpers
        private static float[] SineWave(int length, double frequency = 440.0, double sampleRate = 44100.0)
        {
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = (float)Math.Sin(2.0 * Math.PI * frequency * (i / sampleRate));
            }

            return data;
        }



        // Bad input
        [TestMethod]
        public void Fft_NullData_ReturnsNull()
        {
            this.Fourier.Fft(null!).ShouldBeNull();
        }

        [TestMethod]
        public void Fft_EmptyData_ReturnsNull()
        {
            this.Fourier.Fft(Array.Empty<float>()).ShouldBeNull();
        }

        [TestMethod]
        public void Ifft_EmptyData_ReturnsNull()
        {
            this.Fourier.Ifft(Array.Empty<Vector2>()).ShouldBeNull();
        }

        [TestMethod]
        public void FftChunked_NonMultipleLength_ReturnsNull()
        {
            // Length is not a multiple of the chunk size.
            float[] data = SineWave(ChunkSize + 1);
            this.Fourier.FftChunked(data, ChunkSize).ShouldBeNull();
        }

        [TestMethod]
        public void FftChunked_NonPowerOfTwoChunkSize_ReturnsNull()
        {
            float[] data = new float[300];
            this.Fourier.FftChunked(data, 100).ShouldBeNull();
        }



        // Chunked roundtrip (kernel normalizes on the device)
        [TestMethod]
        public void FftChunked_ThenIfftChunked_ReconstructsSingleChunk()
        {
            float[] data = SineWave(ChunkSize);

            Vector2[]? spectrum = this.Fourier.FftChunked(data, ChunkSize);
            spectrum.ShouldNotBeNull();
            spectrum!.Length.ShouldBe(data.Length);

            float[]? reconstructed = this.Fourier.IfftChunked(spectrum, ChunkSize);
            reconstructed.ShouldNotBeNull();
            reconstructed!.Length.ShouldBe(data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                reconstructed[i].ShouldBe(data[i], 1e-2f, $"chunked mismatch at index {i}");
            }
        }

        [TestMethod]
        public void FftChunked_ThenIfftChunked_ReconstructsMultipleChunks()
        {
            float[] data = SineWave(ChunkSize * 4);

            Vector2[]? spectrum = this.Fourier.FftChunked(data, ChunkSize);
            spectrum.ShouldNotBeNull();
            spectrum!.Length.ShouldBe(data.Length);

            float[]? reconstructed = this.Fourier.IfftChunked(spectrum, ChunkSize);
            reconstructed.ShouldNotBeNull();
            reconstructed!.Length.ShouldBe(data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                reconstructed[i].ShouldBe(data[i], 1e-2f, $"chunked mismatch at index {i}");
            }
        }

        [TestMethod]
        public async Task FftChunkedAsync_ThenIfftChunkedAsync_Reconstructs()
        {
            float[] data = SineWave(ChunkSize);

            Vector2[]? spectrum = await this.Fourier.FftChunkedAsync(data, ChunkSize);
            spectrum.ShouldNotBeNull();

            float[]? reconstructed = await this.Fourier.IfftChunkedAsync(spectrum!, ChunkSize);
            reconstructed.ShouldNotBeNull();
            reconstructed!.Length.ShouldBe(data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                reconstructed[i].ShouldBe(data[i], 1e-2f, $"chunked async mismatch at index {i}");
            }
        }



        // Full (arbitrary-length) roundtrip
        [TestMethod]
        public void Fft_PowerOfTwoLength_ReturnsSameLengthSpectrum()
        {
            float[] data = SineWave(ChunkSize);

            Vector2[]? spectrum = this.Fourier.Fft(data);
            spectrum.ShouldNotBeNull();
            spectrum!.Length.ShouldBe(ChunkSize);
        }

        [TestMethod]
        public void Fft_NonPowerOfTwoLength_PadsToNextPowerOfTwo()
        {
            float[] data = SineWave(1000);

            Vector2[]? spectrum = this.Fourier.Fft(data);
            spectrum.ShouldNotBeNull();
            spectrum!.Length.ShouldBe(OpenClFourier.NextPowerOfTwo(1000)); // 1024
        }

        [TestMethod]
        public void Fft_ThenIfft_ReconstructsPowerOfTwoSignal()
        {
            float[] data = SineWave(ChunkSize);

            Vector2[]? spectrum = this.Fourier.Fft(data);
            spectrum.ShouldNotBeNull();

            float[]? reconstructed = this.Fourier.Ifft(spectrum!, data.Length);
            reconstructed.ShouldNotBeNull();
            reconstructed!.Length.ShouldBe(data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                reconstructed[i].ShouldBe(data[i], 1e-2f, $"full mismatch at index {i}");
            }
        }

        [TestMethod]
        public void Fft_ThenIfft_ReconstructsNonPowerOfTwoSignalWithTrim()
        {
            float[] data = SineWave(1000);

            Vector2[]? spectrum = this.Fourier.Fft(data);
            spectrum.ShouldNotBeNull();

            float[]? reconstructed = this.Fourier.Ifft(spectrum!, data.Length);
            reconstructed.ShouldNotBeNull();
            reconstructed!.Length.ShouldBe(data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                reconstructed[i].ShouldBe(data[i], 1e-2f, $"full trimmed mismatch at index {i}");
            }
        }

        [TestMethod]
        public async Task FftAsync_ThenIfftAsync_Reconstructs()
        {
            float[] data = SineWave(ChunkSize);

            Vector2[]? spectrum = await this.Fourier.FftAsync(data);
            spectrum.ShouldNotBeNull();

            float[]? reconstructed = await this.Fourier.IfftAsync(spectrum!, data.Length);
            reconstructed.ShouldNotBeNull();
            reconstructed!.Length.ShouldBe(data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                reconstructed[i].ShouldBe(data[i], 1e-2f, $"full async mismatch at index {i}");
            }
        }



        // Host-side normalization helper
        [TestMethod]
        public void NormalizeIfftResult_DividesEverySampleByN()
        {
            float[] data = { 4.0f, 8.0f, 16.0f, 32.0f };

            float[]? normalized = OpenClFourier.NormalizeIfftResult(data, 4);
            normalized.ShouldNotBeNull();
            normalized!.ShouldBe(new[] { 1.0f, 2.0f, 4.0f, 8.0f });
        }

        [TestMethod]
        public void NormalizeIfftResult_InvalidInput_ReturnsNull()
        {
            OpenClFourier.NormalizeIfftResult(Array.Empty<float>(), 4).ShouldBeNull();
            OpenClFourier.NormalizeIfftResult(new[] { 1.0f }, 0).ShouldBeNull();
        }

        [TestMethod]
        public void NextPowerOfTwo_ReturnsExpectedValues()
        {
            OpenClFourier.NextPowerOfTwo(1).ShouldBe(1);
            OpenClFourier.NextPowerOfTwo(1000).ShouldBe(1024);
            OpenClFourier.NextPowerOfTwo(1024).ShouldBe(1024);
            OpenClFourier.NextPowerOfTwo(1025).ShouldBe(2048);
        }
    }
}
