using System;
using AsynCUDA12.Runtime;
using ManagedCuda.BasicTypes;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Represents a single column (or a physical component of a complex column) resident in GPU VRAM.
    /// Stores the logical and physical types, element count/size and a reference to the backing
    /// <see cref="CudaMem"/> allocation. The <see cref="IndexPointer"/> is the primary key used to
    /// look the buffer up in the runtime registry and to pass it to kernels.
    /// </summary>
    public sealed class GpuColumn
    {
        /// <summary>Gets the column name.</summary>
        public string Name { get; }

        /// <summary>Gets the logical column type (the type as seen by queries).</summary>
        public GpuColumnType LogicalType { get; }

        /// <summary>Gets the physical column type (the element type actually stored on the device).</summary>
        public GpuColumnType PhysicalType { get; }

        /// <summary>Gets the number of elements in the column.</summary>
        public int ElementCount { get; }

        /// <summary>Gets the size, in bytes, of a single element.</summary>
        public int ElementSize { get; }

        /// <summary>Gets the backing device memory allocation.</summary>
        public CudaMem Memory { get; }

        /// <summary>Gets the native handle (primary key) of the backing allocation.</summary>
        public nint IndexPointer => this.Memory.IndexPointer;

        /// <summary>Gets the CUDA device pointer of the (first) backing buffer.</summary>
        public CUdeviceptr DevicePointer => this.Memory.DevicePointers[0];

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuColumn"/> class.
        /// </summary>
        /// <param name="name">The column name.</param>
        /// <param name="logicalType">The logical column type.</param>
        /// <param name="physicalType">The physical (device) element type.</param>
        /// <param name="elementCount">The number of elements.</param>
        /// <param name="elementSize">The size of one element in bytes.</param>
        /// <param name="memory">The backing device memory allocation.</param>
        public GpuColumn(string name, GpuColumnType logicalType, GpuColumnType physicalType, int elementCount, int elementSize, CudaMem memory)
        {
            this.Name = name;
            this.LogicalType = logicalType;
            this.PhysicalType = physicalType;
            this.ElementCount = elementCount;
            this.ElementSize = elementSize;
            this.Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }
    }
}
