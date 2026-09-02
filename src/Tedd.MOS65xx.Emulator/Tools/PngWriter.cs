using System;
using System.IO;
using System.IO.Compression;

namespace Tedd.MOS65xx.Emulator.Tools;

/// <summary>
/// Minimal PNG encoder (8 bit RGB, no alpha, no interlacing) with no dependencies beyond
/// <see cref="ZLibStream"/>. Used by the tests to dump VIC-II frames and by the GUI screenshot feature.
/// Format per the PNG specification (ISO/IEC 15948): signature, IHDR, IDAT (zlib compressed scanlines, each
/// prefixed with filter type 0 = None), IEND. Every chunk carries a CRC-32 over its type and data.
/// </summary>
public static class PngWriter
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    /// <summary>CRC-32 (IEEE 802.3 polynomial, as used by PNG and zip) of <paramref name="data"/>.</summary>
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in data)
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    /// <summary>
    /// Encodes a <paramref name="width"/> x <paramref name="height"/> image whose pixels are given as
    /// 0xAARRGGBB (alpha ignored), row-major, into a complete PNG file image.
    /// </summary>
    public static byte[] Encode(int width, int height, ReadOnlySpan<uint> argb)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
        if (argb.Length < width * height)
            throw new ArgumentException("Pixel buffer is smaller than width * height.", nameof(argb));

        // Raw scanlines: one filter byte (0 = None) followed by width RGB triples.
        var raw = new byte[height * (1 + width * 3)];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            raw[o++] = 0;
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                uint p = argb[row + x];
                raw[o++] = (byte)(p >> 16);
                raw[o++] = (byte)(p >> 8);
                raw[o++] = (byte)p;
            }
        }

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                z.Write(raw, 0, raw.Length);
            compressed = ms.ToArray();
        }

        using var output = new MemoryStream(8 + 25 + 12 + compressed.Length + 12);
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        Span<byte> ihdr = stackalloc byte[13];
        WriteBigEndian(ihdr, 0, (uint)width);
        WriteBigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 2;   // color type: truecolor (RGB)
        ihdr[10] = 0;  // compression method: deflate
        ihdr[11] = 0;  // filter method: adaptive (per scanline filter byte)
        ihdr[12] = 0;  // interlace: none
        WriteChunk(output, "IHDR", ihdr);
        WriteChunk(output, "IDAT", compressed);
        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    /// <summary>Encodes the image and writes it to <paramref name="path"/> (the directory is created if needed).</summary>
    public static void Save(string path, int width, int height, ReadOnlySpan<uint> argb)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, Encode(width, height, argb));
    }

    private static void WriteChunk(Stream s, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        WriteBigEndian(len, 0, (uint)data.Length);
        s.Write(len);

        var typeAndData = new byte[4 + data.Length];
        typeAndData[0] = (byte)type[0];
        typeAndData[1] = (byte)type[1];
        typeAndData[2] = (byte)type[2];
        typeAndData[3] = (byte)type[3];
        data.CopyTo(typeAndData.AsSpan(4));
        s.Write(typeAndData);

        Span<byte> crc = stackalloc byte[4];
        WriteBigEndian(crc, 0, Crc32(typeAndData));
        s.Write(crc);
    }

    private static void WriteBigEndian(Span<byte> buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
