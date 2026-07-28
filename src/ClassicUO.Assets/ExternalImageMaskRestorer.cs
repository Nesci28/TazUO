using System;

namespace ClassicUO.Assets
{
    public static class ExternalImageMaskRestorer
    {
        public static void RestoreFromOriginal(
            ReadOnlySpan<uint> originalPixels,
            int originalWidth,
            int originalHeight,
            Span<uint> externalPixels,
            int externalWidth,
            int externalHeight,
            int sourceScale,
            bool restoreGrayscaleMask = true
        )
        {
            for (int y = 0; y < externalHeight; y++)
            {
                int originalY = Math.Min(originalHeight - 1, y / sourceScale);

                for (int x = 0; x < externalWidth; x++)
                {
                    int externalIndex = y * externalWidth + x;
                    int originalX = Math.Min(originalWidth - 1, x / sourceScale);
                    uint original = originalPixels[originalY * originalWidth + originalX];

                    if (original == 0)
                    {
                        externalPixels[externalIndex] = 0;
                        continue;
                    }

                    uint pixel = externalPixels[externalIndex];
                    uint alpha = pixel & 0xFF000000;
                    if (alpha == 0)
                        alpha = 0xFF000000;

                    byte originalR = (byte)original;
                    byte originalG = (byte)(original >> 8);
                    byte originalB = (byte)(original >> 16);

                    if (
                        restoreGrayscaleMask
                        && originalR == originalG
                        && originalR == originalB
                    )
                    {
                        uint gray = (uint)(
                            ((byte)pixel + (byte)(pixel >> 8) + (byte)(pixel >> 16)) / 3
                        );
                        pixel = gray | (gray << 8) | (gray << 16);
                    }

                    externalPixels[externalIndex] = (pixel & 0x00FFFFFF) | alpha;
                }
            }
        }
    }
}
