using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AsynCUDA12.GpuDatabase
{
    /// <summary>
    /// Reads and writes the on-disk GPU database file format ("GPDB"). The format is intentionally
    /// simple, GPU-friendly and column-oriented: numeric columns are stored as contiguous arrays so
    /// they can be loaded straight into VRAM, and string columns are stored as a UTF-8 byte buffer plus
    /// integer offsets and lengths. Writes are atomic (temp file + replace).
    /// </summary>
    public sealed class PersistenceManager
    {
        private const uint Magic = 0x42445047; // "GPDB" little-endian
        private const int FormatVersion = 1;

        /// <summary>
        /// Loads a host database from the given file path.
        /// </summary>
        /// <param name="path">The database file path.</param>
        /// <returns>The parsed <see cref="HostDatabase"/>.</returns>
        public HostDatabase Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

            uint magic = reader.ReadUInt32();
            if (magic != Magic)
            {
                throw new InvalidDataException($"Not a GPDB file (bad magic 0x{magic:X8}).");
            }

            int version = reader.ReadInt32();
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported GPDB version {version}.");
            }

            int tableCount = reader.ReadInt32();
            HostDatabase database = new();
            for (int t = 0; t < tableCount; t++)
            {
                database.Add(ReadTable(reader));
            }

            return database;
        }

        /// <summary>
        /// Atomically saves a host database to the given file path (writes to a temp file, then replaces).
        /// </summary>
        /// <param name="path">The destination database file path.</param>
        /// <param name="database">The database to persist.</param>
        public void Save(string path, HostDatabase database)
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = path + ".tmp";
            using (FileStream stream = File.Create(tempPath))
            using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(database.Tables.Count);
                foreach (HostTable table in database.Tables)
                {
                    WriteTable(writer, table);
                }
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        // ---- Table I/O ----

        private static HostTable ReadTable(BinaryReader reader)
        {
            string name = reader.ReadString();
            int rowCount = reader.ReadInt32();
            int columnCount = reader.ReadInt32();

            HostTable table = new(name, rowCount);
            for (int c = 0; c < columnCount; c++)
            {
                table.Add(ReadColumn(reader, rowCount));
            }

            return table;
        }

        private static void WriteTable(BinaryWriter writer, HostTable table)
        {
            writer.Write(table.Name);
            writer.Write(table.RowCount);
            writer.Write(table.Columns.Count);
            foreach (HostColumn column in table.Columns)
            {
                WriteColumn(writer, column);
            }
        }

        // ---- Column I/O ----

        private static HostColumn ReadColumn(BinaryReader reader, int rowCount)
        {
            string name = reader.ReadString();
            GpuColumnType type = (GpuColumnType) reader.ReadByte();

            if (type == GpuColumnType.String)
            {
                return ReadStringColumn(reader, name);
            }

            int elementCount = reader.ReadInt32();
            return HostColumn.Numeric(name, type, ReadNumericArray(reader, type, elementCount));
        }

        private static void WriteColumn(BinaryWriter writer, HostColumn column)
        {
            writer.Write(column.Name);
            writer.Write((byte) column.Type);

            if (column.Type == GpuColumnType.String)
            {
                WriteStringColumn(writer, column);
                return;
            }

            writer.Write(column.RowCount);
            WriteNumericArray(writer, column);
        }

        private static Array ReadNumericArray(BinaryReader reader, GpuColumnType type, int count)
        {
            switch (type)
            {
                case GpuColumnType.Int32:
                    int[] ints = new int[count];
                    for (int i = 0; i < count; i++) { ints[i] = reader.ReadInt32(); }
                    return ints;
                case GpuColumnType.Int64:
                    long[] longs = new long[count];
                    for (int i = 0; i < count; i++) { longs[i] = reader.ReadInt64(); }
                    return longs;
                case GpuColumnType.Single:
                    float[] floats = new float[count];
                    for (int i = 0; i < count; i++) { floats[i] = reader.ReadSingle(); }
                    return floats;
                case GpuColumnType.Double:
                    double[] doubles = new double[count];
                    for (int i = 0; i < count; i++) { doubles[i] = reader.ReadDouble(); }
                    return doubles;
                case GpuColumnType.Byte:
                    return reader.ReadBytes(count);
                default:
                    throw new InvalidDataException($"Unsupported numeric column type {type}.");
            }
        }

        private static void WriteNumericArray(BinaryWriter writer, HostColumn column)
        {
            switch (column.Type)
            {
                case GpuColumnType.Int32:
                    foreach (int v in column.AsArray<int>()) { writer.Write(v); }
                    break;
                case GpuColumnType.Int64:
                    foreach (long v in column.AsArray<long>()) { writer.Write(v); }
                    break;
                case GpuColumnType.Single:
                    foreach (float v in column.AsArray<float>()) { writer.Write(v); }
                    break;
                case GpuColumnType.Double:
                    foreach (double v in column.AsArray<double>()) { writer.Write(v); }
                    break;
                case GpuColumnType.Byte:
                    writer.Write(column.AsArray<byte>());
                    break;
                default:
                    throw new InvalidDataException($"Unsupported numeric column type {column.Type}.");
            }
        }

        private static HostColumn ReadStringColumn(BinaryReader reader, string name)
        {
            int rowCount = reader.ReadInt32();
            int byteLength = reader.ReadInt32();
            byte[] data = reader.ReadBytes(byteLength);

            int[] offsets = new int[rowCount];
            for (int i = 0; i < rowCount; i++) { offsets[i] = reader.ReadInt32(); }

            int[] lengths = new int[rowCount];
            for (int i = 0; i < rowCount; i++) { lengths[i] = reader.ReadInt32(); }

            return HostColumn.Strings(name, DecodeStrings(data, offsets, lengths));
        }

        private static void WriteStringColumn(BinaryWriter writer, HostColumn column)
        {
            (byte[] data, int[] offsets, int[] lengths) = EncodeStrings(column.AsStrings());

            writer.Write(column.RowCount);
            writer.Write(data.Length);
            writer.Write(data);
            foreach (int offset in offsets) { writer.Write(offset); }
            foreach (int length in lengths) { writer.Write(length); }
        }

        // ---- String transforms ----

        /// <summary>
        /// Encodes a list of strings into a single contiguous UTF-8 byte buffer plus per-row
        /// start offsets and byte lengths (the GPU-friendly string layout).
        /// </summary>
        /// <param name="values">The string values to encode.</param>
        /// <returns>A tuple of the byte data, the offsets and the lengths.</returns>
        public static (byte[] data, int[] offsets, int[] lengths) EncodeStrings(IReadOnlyList<string> values)
        {
            int[] offsets = new int[values.Count];
            int[] lengths = new int[values.Count];

            using MemoryStream buffer = new();
            int cursor = 0;
            for (int i = 0; i < values.Count; i++)
            {
                byte[] encoded = Encoding.UTF8.GetBytes(values[i] ?? string.Empty);
                offsets[i] = cursor;
                lengths[i] = encoded.Length;
                buffer.Write(encoded, 0, encoded.Length);
                cursor += encoded.Length;
            }

            return (buffer.ToArray(), offsets, lengths);
        }

        /// <summary>
        /// Reconstructs the original strings from a UTF-8 byte buffer plus offsets and lengths.
        /// </summary>
        /// <param name="data">The contiguous UTF-8 byte buffer.</param>
        /// <param name="offsets">The per-row start offsets into <paramref name="data"/>.</param>
        /// <param name="lengths">The per-row byte lengths.</param>
        /// <returns>The decoded string array.</returns>
        public static string[] DecodeStrings(byte[] data, int[] offsets, int[] lengths)
        {
            string[] result = new string[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                result[i] = Encoding.UTF8.GetString(data, offsets[i], lengths[i]);
            }

            return result;
        }
    }
}
