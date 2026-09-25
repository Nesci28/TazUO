using System;
using System.IO;

namespace ClassicUO.Utility
{
    // UO's row-offset/RLE format, shared by legacy MUL and decoded UOP gumps.
    internal static class GumpPixels
    {
        public static uint[] Decode(byte[] data, int start, int width, int height,
                                    Func<ushort, uint> convertColor)
        {
            // Modern gump archives keep empty entries as an 8-byte 0x0 header.
            if (width == 0 && height == 0 && start == data.Length)
                return Array.Empty<uint>();
            if (width <= 0 || height <= 0 || (long)width * height > 16777216)
                throw new InvalidDataException("Invalid gump dimensions");
            if (start < 0 || start > data.Length || height > (data.Length - start) / 4)
                throw new InvalidDataException("Truncated gump row table");

            var pixels = new uint[width * height];
            for (int y = 0; y < height; y++)
            {
                long offset = start + (long)BitConverter.ToUInt32(data, start + y * 4) * 4;
                long end = y + 1 < height
                    ? start + (long)BitConverter.ToUInt32(data, start + (y + 1) * 4) * 4
                    : data.Length;
                if (offset < start + height * 4 || end < offset || end > data.Length)
                    throw new InvalidDataException("Invalid gump row offset");
                int x = 0;
                while (offset + 4 <= end)
                {
                    ushort value = BitConverter.ToUInt16(data, (int)offset);
                    int run = BitConverter.ToUInt16(data, (int)offset + 2);
                    if (run > width - x)
                        throw new InvalidDataException("Gump run exceeds row width");
                    uint rgba = value == 0 ? 0 : convertColor(value);
                    for (int i = 0; i < run; i++) pixels[y * width + x++] = rgba;
                    offset += 4;
                }
                if (offset != end)
                    throw new InvalidDataException("Truncated gump run");
            }
            return pixels;
        }
    }
}
