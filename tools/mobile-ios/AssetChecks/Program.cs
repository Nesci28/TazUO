using System.IO.Compression;
using ClassicUO.Utility;

// Minimal 2x2 legacy RLE: one opaque run, one transparent run.
byte[] small = [2,0,0,0,3,0,0,0,31,0,2,0,0,0,2,0];
var pixels = GumpPixels.Decode(small, 0, 2, 2, value => value);
if (!pixels.SequenceEqual(new uint[] {31,31,0,0})) throw new Exception("RLE pixel mismatch");
foreach (var invalid in new[] {small[..5], new byte[] {255,255,255,127,3,0,0,0,31,0,3,0,0,0,2,0}})
{
    try { GumpPixels.Decode(invalid, 0, 2, 2, value => value); }
    catch (InvalidDataException) { continue; }
    throw new Exception("Invalid gump was accepted");
}
Console.WriteLine("Legacy pixels and malformed row checks passed.");
if (args.Length == 0) return;

var cliloc = File.ReadAllBytes(Path.Combine(args[0], "Cliloc.enu"));
if (cliloc[3] == 0x8e) cliloc = BwtDecompress.Decompress(cliloc);
using (var r = new BinaryReader(new MemoryStream(cliloc)))
{
    r.ReadInt32(); r.ReadInt16(); int strings = 0;
    while (r.BaseStream.Position < r.BaseStream.Length)
    {
        r.ReadInt32(); r.ReadByte(); int length = r.ReadInt16();
        if (length < 0 || r.ReadBytes(length).Length != length) throw new Exception("Invalid Cliloc entry");
        strings++;
    }
    Console.WriteLine($"Decoded {strings} real Cliloc entries.");
}
using var file = File.OpenRead(Path.Combine(args[0], "gumpartLegacyMUL.uop"));
using var reader = new BinaryReader(file);
file.Position = 12; long block = reader.ReadInt64(); int count = 0;
while (block != 0)
{
    file.Position = block; int entries = reader.ReadInt32(); block = reader.ReadInt64();
    for (int i = 0; i < entries; i++)
    {
        long offset = reader.ReadInt64(); int header = reader.ReadInt32();
        int compressed = reader.ReadInt32(); int expanded = reader.ReadInt32();
        reader.ReadUInt64(); reader.ReadUInt32(); int flag = reader.ReadInt16();
        long next = file.Position;
        if (offset == 0) continue;
        file.Position = offset + header;
        byte[] bytes = reader.ReadBytes(compressed);
        if (flag != 0)
        {
            using var z = new ZLibStream(new MemoryStream(bytes), CompressionMode.Decompress);
            using var output = new MemoryStream(); z.CopyTo(output); bytes = output.ToArray();
            if (bytes.Length != expanded) throw new Exception("UOP expanded length mismatch");
            if (flag == 3) bytes = BwtDecompress.Decompress(bytes);
        }
        int w = BitConverter.ToInt32(bytes, 0), h = BitConverter.ToInt32(bytes, 4);
        try { GumpPixels.Decode(bytes, 8, w, h, value => value); }
        catch (Exception e) { throw new Exception($"Gump {count}, offset={offset}, flag={flag}, size={w}x{h}, bytes={bytes.Length}", e); }
        count++; file.Position = next;
    }
}
Console.WriteLine($"Decoded {count} real UOP gumps with valid dimensions and rows.");
