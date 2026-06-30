using System;
using AsynCUDA.OpenCl;

namespace AsynCUDA.ClDatabase
{
    /// <summary>
    /// Represents a single column (or a physical component of a complex column) resident in OpenCL device
    /// memory. Stores the logical and physical types, element count/size and a reference to the backing
    /// <see cref="OpenClMem"/> allocation. The <see cref="Memory"/> object is passed directly to kernels
    /// through the launcher.
    /// </summary>
    public sealed class ClColumn
    {
        /// <summary>Gets the column name.</summary>
        public string Name { get; }

        /// <summary>Gets the logical column type (the type as seen by queries).</summary>
        public ClColumnType LogicalType { get; }

        /// <summary>Gets the physical column type (the element type actually stored on the device).</summary>
        public ClColumnType PhysicalType { get; }

        /// <summary>Gets the number of elements in the column.</summary>
        public int ElementCount { get; }

        /// <summary>Gets the size, in bytes, of a single element.</summary>
        public int ElementSize { get; }

        /// <summary>Gets the backing device memory allocation.</summary>
        public OpenClMem Memory { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClColumn"/> class.
        /// </summary>
        /// <param name="name">The column name.</param>
        /// <param name="logicalType">The logical column type.</param>
        /// <param name="physicalType">The physical (device) element type.</param>
        /// <param name="elementCount">The number of elements.</param>
        /// <param name="elementSize">The size of one element in bytes.</param>
        /// <param name="memory">The backing device memory allocation.</param>
        public ClColumn(string name, ClColumnType logicalType, ClColumnType physicalType, int elementCount, int elementSize, OpenClMem memory)
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
