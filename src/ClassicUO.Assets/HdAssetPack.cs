using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClassicUO.Assets
{
    public enum HdAssetKind : byte
    {
        Gump = 1,
        Art = 2,
        Land = 3,
        Texmap = 4,
        Animation = 5
    }

    public readonly struct HdAssetPackSource
    {
        public HdAssetPackSource(HdAssetKind kind, ulong key, int sourceScale, string path)
        {
            Kind = kind;
            Key = key;
            SourceScale = sourceScale;
            Path = path;
        }

        public HdAssetKind Kind { get; }
        public ulong Key { get; }
        public int SourceScale { get; }
        public string Path { get; }
    }

    internal readonly struct HdAssetPackEntry
    {
        public HdAssetPackEntry(
            HdAssetKind kind,
            ulong key,
            int sourceScale,
            long offset,
            int length,
            uint crc32
        )
        {
            Kind = kind;
            Key = key;
            SourceScale = sourceScale;
            Offset = offset;
            Length = length;
            Crc32 = crc32;
        }

        public HdAssetKind Kind { get; }
        public ulong Key { get; }
        public int SourceScale { get; }
        public long Offset { get; }
        public int Length { get; }
        public uint Crc32 { get; }
    }

    internal static class HdAssetPackFormat
    {
        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("TUOHDPK\0");
        public const uint Version = 1;
        public const int HeaderSize = 32;
        public const int EntrySize = 32;
        public const int MaxEntries = 10_000_000;
        public const int MaxEncodedImageSize = 512 * 1024 * 1024;

        public static bool IsValidKind(HdAssetKind kind) =>
            kind is HdAssetKind.Gump
                or HdAssetKind.Art
                or HdAssetKind.Land
                or HdAssetKind.Texmap
                or HdAssetKind.Animation;

        public static bool IsValidScale(int sourceScale) => sourceScale is 2 or 4;
    }

    internal static class HdAssetPackCrc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Start() => uint.MaxValue;

        public static uint Append(uint crc, byte[] buffer, int count)
        {
            for (int i = 0; i < count; i++)
                crc = Table[(byte)(crc ^ buffer[i])] ^ (crc >> 8);
            return crc;
        }

        public static uint Finish(uint crc) => ~crc;

        public static uint Compute(byte[] bytes) => Finish(Append(Start(), bytes, bytes.Length));

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xEDB88320U ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }
    }

    internal sealed class HdAssetPack : IDisposable
    {
        private readonly FileStream _stream;
        private readonly object _streamLock = new object();

        private HdAssetPack(string path, FileStream stream, List<HdAssetPackEntry> entries)
        {
            Path = path;
            _stream = stream;
            Entries = entries;
        }

        public string Path { get; }
        public IReadOnlyList<HdAssetPackEntry> Entries { get; }

        public static HdAssetPack Open(string path)
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                64 * 1024,
                FileOptions.RandomAccess
            );

            try
            {
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                byte[] magic = reader.ReadBytes(HdAssetPackFormat.Magic.Length);
                if (!magic.SequenceEqual(HdAssetPackFormat.Magic))
                    throw new InvalidDataException("Invalid HD asset pack signature.");

                uint version = reader.ReadUInt32();
                if (version != HdAssetPackFormat.Version)
                    throw new InvalidDataException($"Unsupported HD asset pack version: {version}.");

                uint headerSize = reader.ReadUInt32();
                long entryCount = reader.ReadInt64();
                long indexOffset = reader.ReadInt64();
                if (
                    headerSize != HdAssetPackFormat.HeaderSize
                    || indexOffset < headerSize
                    || entryCount <= 0
                    || entryCount > HdAssetPackFormat.MaxEntries
                )
                {
                    throw new InvalidDataException("Invalid HD asset pack header.");
                }

                long dataOffset = checked(indexOffset + entryCount * HdAssetPackFormat.EntrySize);
                if (dataOffset > stream.Length)
                    throw new InvalidDataException("The HD asset pack index is truncated.");

                stream.Position = indexOffset;
                var entries = new List<HdAssetPackEntry>((int)entryCount);
                HdAssetKind previousKind = 0;
                ulong previousKey = 0;
                for (long i = 0; i < entryCount; i++)
                {
                    var kind = (HdAssetKind)reader.ReadByte();
                    int sourceScale = reader.ReadByte();
                    ushort flags = reader.ReadUInt16();
                    ulong key = reader.ReadUInt64();
                    long offset = reader.ReadInt64();
                    long length = reader.ReadInt64();
                    uint crc32 = reader.ReadUInt32();

                    if (
                        !HdAssetPackFormat.IsValidKind(kind)
                        || !HdAssetPackFormat.IsValidScale(sourceScale)
                        || flags != 0
                        || offset < dataOffset
                        || length <= 0
                        || length > HdAssetPackFormat.MaxEncodedImageSize
                        || offset > stream.Length - length
                    )
                    {
                        throw new InvalidDataException($"Invalid HD asset pack entry at index {i}.");
                    }
                    if (
                        i > 0
                        && (
                            (byte)kind < (byte)previousKind
                            || (kind == previousKind && key <= previousKey)
                        )
                    )
                    {
                        throw new InvalidDataException(
                            "HD asset pack entries must be uniquely sorted by kind and key."
                        );
                    }

                    entries.Add(
                        new HdAssetPackEntry(kind, key, sourceScale, offset, (int)length, crc32)
                    );
                    previousKind = kind;
                    previousKey = key;
                }

                return new HdAssetPack(fullPath, stream, entries);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public byte[] Read(HdAssetPackEntry entry)
        {
            var bytes = new byte[entry.Length];
            lock (_streamLock)
            {
                _stream.Position = entry.Offset;
                int totalRead = 0;
                while (totalRead < bytes.Length)
                {
                    int read = _stream.Read(bytes, totalRead, bytes.Length - totalRead);
                    if (read == 0)
                        throw new EndOfStreamException($"HD asset pack entry is truncated: {Path}");
                    totalRead += read;
                }
            }

            uint crc32 = HdAssetPackCrc32.Compute(bytes);
            if (crc32 != entry.Crc32)
                throw new InvalidDataException($"HD asset pack entry checksum failed: {Path}");
            return bytes;
        }

        public bool TryGetEntry(HdAssetKind kind, ulong key, out HdAssetPackEntry entry)
        {
            int low = 0;
            int high = Entries.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                HdAssetPackEntry candidate = Entries[middle];
                int comparison = candidate.Kind.CompareTo(kind);
                if (comparison == 0)
                    comparison = candidate.Key.CompareTo(key);

                if (comparison < 0)
                    low = middle + 1;
                else if (comparison > 0)
                    high = middle - 1;
                else
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public void Dispose() => _stream.Dispose();
    }

    public static class HdAssetPackWriter
    {
        private readonly struct WrittenEntry
        {
            public WrittenEntry(HdAssetPackSource source, long offset, long length, uint crc32)
            {
                Source = source;
                Offset = offset;
                Length = length;
                Crc32 = crc32;
            }

            public HdAssetPackSource Source { get; }
            public long Offset { get; }
            public long Length { get; }
            public uint Crc32 { get; }
        }

        public static void Write(
            string outputPath,
            IEnumerable<HdAssetPackSource> assets,
            Action<int, int, long> progress = null
        )
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("An output path is required.", nameof(outputPath));
            if (assets == null)
                throw new ArgumentNullException(nameof(assets));

            List<HdAssetPackSource> sources = assets
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Key)
                .ThenBy(x => x.Path, StringComparer.Ordinal)
                .ToList();
            if (sources.Count == 0)
                throw new InvalidDataException("Cannot create an empty HD asset pack.");
            if (sources.Count > HdAssetPackFormat.MaxEntries)
                throw new InvalidDataException("The HD asset pack contains too many entries.");

            var keys = new HashSet<(HdAssetKind kind, ulong key)>();
            foreach (HdAssetPackSource source in sources)
            {
                if (!HdAssetPackFormat.IsValidKind(source.Kind))
                    throw new InvalidDataException($"Invalid HD asset kind: {source.Kind}.");
                if (!HdAssetPackFormat.IsValidScale(source.SourceScale))
                    throw new InvalidDataException($"Invalid HD asset scale: {source.SourceScale}.");
                if (string.IsNullOrWhiteSpace(source.Path) || !File.Exists(source.Path))
                    throw new FileNotFoundException("HD asset source is missing.", source.Path);
                if (!keys.Add((source.Kind, source.Key)))
                    throw new InvalidDataException(
                        $"Duplicate HD asset key: {source.Kind}/{source.Key}."
                    );
            }

            string fullOutputPath = System.IO.Path.GetFullPath(outputPath);
            string outputDirectory = System.IO.Path.GetDirectoryName(fullOutputPath);
            Directory.CreateDirectory(outputDirectory);
            string temporaryPath = fullOutputPath + ".tmp";
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            try
            {
                using (
                    var stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1024 * 1024,
                        FileOptions.SequentialScan
                    )
                )
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    long indexOffset = HdAssetPackFormat.HeaderSize;
                    long dataOffset = checked(
                        indexOffset + (long)sources.Count * HdAssetPackFormat.EntrySize
                    );

                    writer.Write(HdAssetPackFormat.Magic);
                    writer.Write(HdAssetPackFormat.Version);
                    writer.Write((uint)HdAssetPackFormat.HeaderSize);
                    writer.Write((long)sources.Count);
                    writer.Write(indexOffset);
                    stream.Position = dataOffset;

                    var entries = new List<WrittenEntry>(sources.Count);
                    var buffer = new byte[1024 * 1024];
                    for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                    {
                        HdAssetPackSource source = sources[sourceIndex];
                        long offset = stream.Position;
                        long length = 0;
                        uint crc = HdAssetPackCrc32.Start();
                        using var input = new FileStream(
                            source.Path,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            buffer.Length,
                            FileOptions.SequentialScan
                        );

                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            length += read;
                            if (length > HdAssetPackFormat.MaxEncodedImageSize)
                                throw new InvalidDataException(
                                    $"HD asset is too large: {source.Path}"
                                );
                            stream.Write(buffer, 0, read);
                            crc = HdAssetPackCrc32.Append(crc, buffer, read);
                        }

                        if (length == 0)
                            throw new InvalidDataException($"HD asset is empty: {source.Path}");
                        entries.Add(
                            new WrittenEntry(source, offset, length, HdAssetPackCrc32.Finish(crc))
                        );
                        int completed = sourceIndex + 1;
                        if (completed % 1000 == 0 || completed == sources.Count)
                            progress?.Invoke(completed, sources.Count, stream.Position - dataOffset);
                    }

                    stream.Position = indexOffset;
                    foreach (WrittenEntry entry in entries)
                    {
                        writer.Write((byte)entry.Source.Kind);
                        writer.Write((byte)entry.Source.SourceScale);
                        writer.Write((ushort)0);
                        writer.Write(entry.Source.Key);
                        writer.Write(entry.Offset);
                        writer.Write(entry.Length);
                        writer.Write(entry.Crc32);
                    }

                    writer.Flush();
                    stream.Flush(true);
                }

                File.Move(temporaryPath, fullOutputPath, true);
            }
            catch
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                throw;
            }
        }
    }
}
