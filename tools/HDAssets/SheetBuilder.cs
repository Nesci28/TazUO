using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Tools.HDAssets;

internal sealed class SheetBuilder : IDisposable
{
    private readonly string _directory;
    private readonly int _sheetSize;
    private readonly int _padding;
    private Image<Rgba32> _image;
    private int _sheetIndex;
    private int _cursorX;
    private int _cursorY;
    private int _rowHeight;
    private string _sheetName;

    public SheetBuilder(string directory, int sheetSize, int padding)
    {
        _directory = directory;
        _sheetSize = sheetSize;
        _padding = padding;
        Directory.CreateDirectory(directory);
    }

    public void Place(Rgba32[] pixels, int width, int height, AssetEntry entry)
    {
        int cellWidth = width + _padding * 2;
        int cellHeight = height + _padding * 2;

        if (cellWidth > _sheetSize || cellHeight > _sheetSize)
        {
            Flush();
            CreateSheet(cellWidth, cellHeight);
            WriteCell(pixels, width, height, 0, 0);
            entry.X = _padding;
            entry.Y = _padding;
            entry.Sheet = _sheetName;
            Flush();
            return;
        }

        EnsureStandardSheet();

        if (_cursorX + cellWidth > _image.Width)
        {
            _cursorX = 0;
            _cursorY += _rowHeight;
            _rowHeight = 0;
        }

        if (_cursorY + cellHeight > _image.Height)
        {
            Flush();
            EnsureStandardSheet();
        }

        WriteCell(pixels, width, height, _cursorX, _cursorY);
        entry.X = _cursorX + _padding;
        entry.Y = _cursorY + _padding;
        entry.Sheet = _sheetName;

        _cursorX += cellWidth;
        _rowHeight = Math.Max(_rowHeight, cellHeight);
    }

    public void Finish() => Flush();

    private void EnsureStandardSheet()
    {
        if (_image == null)
            CreateSheet(_sheetSize, _sheetSize);
    }

    private void CreateSheet(int width, int height)
    {
        _sheetName = $"sheet-{_sheetIndex++:D6}.png";
        _image = new Image<Rgba32>(width, height);
        _image.ProcessPixelRows(accessor =>
        {
            var background = new Rgba32(0, 0, 0, byte.MaxValue);
            for (int y = 0; y < height; y++)
                accessor.GetRowSpan(y).Fill(background);
        });
        _cursorX = 0;
        _cursorY = 0;
        _rowHeight = 0;
    }

    private void WriteCell(
        Rgba32[] pixels,
        int width,
        int height,
        int destinationX,
        int destinationY
    )
    {
        int cellWidth = width + _padding * 2;
        int cellHeight = height + _padding * 2;

        _image.ProcessPixelRows(accessor =>
        {
            for (int cellY = 0; cellY < cellHeight; cellY++)
            {
                int sourceY = Math.Clamp(cellY - _padding, 0, height - 1);
                Span<Rgba32> destinationRow = accessor
                    .GetRowSpan(destinationY + cellY)
                    .Slice(destinationX, cellWidth);

                for (int cellX = 0; cellX < cellWidth; cellX++)
                {
                    int sourceX = Math.Clamp(cellX - _padding, 0, width - 1);
                    destinationRow[cellX] = pixels[sourceY * width + sourceX];
                }
            }
        });
    }

    private void Flush()
    {
        if (_image == null)
            return;

        string path = Path.Combine(_directory, _sheetName);
        _image.SaveAsPng(path);
        _image.Dispose();
        _image = null;
    }

    public void Dispose()
    {
        Flush();
    }
}
