/// <summary>The two icon container formats, written by hand so the tool runs the same on Windows and macOS (no iconutil, no sips).</summary>
internal static class IconContainers
{
    /// <summary>
    /// A Windows .ico holding one PNG per size (supported since Windows Vista): a 6-byte header, one 16-byte directory
    /// entry per image (width and height 0 mean 256), then the PNG data in order. Little-endian.
    /// </summary>
    public static byte[] Ico(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)images.Count);
        var offset = 6 + (16 * images.Count);
        foreach (var (size, png) in images)
        {
            var side = (byte)(size >= 256 ? 0 : size);
            writer.Write(side);
            writer.Write(side);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in images)
        {
            writer.Write(png);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// A macOS .icns of PNG entries: the magic <c>icns</c> and the big-endian total length, then per entry its 4-character
    /// type and the big-endian length including that 8-byte header, then the PNG data.
    /// </summary>
    public static byte[] Icns(IReadOnlyList<(string Type, byte[] Png)> entries)
    {
        using var stream = new MemoryStream();
        var total = 8 + entries.Sum(entry => 8 + entry.Png.Length);
        WriteAscii(stream, "icns");
        WriteBigEndian(stream, total);
        foreach (var (type, png) in entries)
        {
            WriteAscii(stream, type);
            WriteBigEndian(stream, 8 + png.Length);
            stream.Write(png);
        }

        return stream.ToArray();
    }

    private static void WriteAscii(Stream stream, string text) => stream.Write(System.Text.Encoding.ASCII.GetBytes(text));

    private static void WriteBigEndian(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
