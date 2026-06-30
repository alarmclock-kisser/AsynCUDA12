using System;
using System.Collections.Generic;
using ManagedCuda.BasicTypes;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Represents an ordered sequence of kernel invocations that together implement a query (filter,
    /// projection, aggregation, …). Steps are recorded against device buffers and then executed in order
    /// on the default stream via the <see cref="GpuKernelExecutor"/>.
    /// </summary>
    public sealed class GpuKernelPipeline
    {
        private readonly GpuKernelExecutor executor;
        private readonly List<PipelineStep> steps = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuKernelPipeline"/> class.
        /// </summary>
        /// <param name="executor">The executor used to launch the recorded kernels.</param>
        public GpuKernelPipeline(GpuKernelExecutor executor)
        {
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>Gets the number of recorded steps.</summary>
        public int StepCount => this.steps.Count;

        /// <summary>
        /// Appends a kernel step to the pipeline.
        /// </summary>
        /// <param name="kernelName">The kernel to run.</param>
        /// <param name="pointers">The device pointers, in kernel-signature order.</param>
        /// <param name="scalars">The scalar arguments, in kernel-signature order.</param>
        /// <param name="length">The linear work length for this step.</param>
        /// <returns>This pipeline instance for fluent chaining.</returns>
        public GpuKernelPipeline Add(string kernelName, IReadOnlyList<CUdeviceptr> pointers, IReadOnlyList<object> scalars, int length)
        {
            this.steps.Add(new PipelineStep(kernelName, pointers, scalars, length));
            return this;
        }

        /// <summary>
        /// Executes all recorded steps in order, stopping at the first failure.
        /// </summary>
        /// <returns><c>true</c> if all steps succeeded; otherwise <c>false</c>.</returns>
        public bool Run()
        {
            foreach (PipelineStep step in this.steps)
            {
                if (!this.executor.Execute(step.KernelName, step.Pointers, step.Scalars, step.Length))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed record PipelineStep(string KernelName, IReadOnlyList<CUdeviceptr> Pointers, IReadOnlyList<object> Scalars, int Length);
    }
}
