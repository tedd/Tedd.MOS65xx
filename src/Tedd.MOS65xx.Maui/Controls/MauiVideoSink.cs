using Tedd.Maui;
using Tedd.MOS65xx.Emulator.Video;
using Tedd.MOS65xx.Hosting;

namespace Tedd.MOS65xx.Maui.Controls;

/// <summary>
/// <see cref="IVideoSink"/> for MAUI, on top of <see cref="Tedd.Maui.WriteableBitmap"/>: the emulator thread
/// leases one of the bitmap's native back buffers, writes the 384 x 272 picture into it and publishes it, which
/// asks every attached <see cref="WriteableBitmapView"/> for one redraw. Nothing else happens on either thread -
/// the view hands the buffer to Skia as a texture and the GPU does the magnification with nearest neighbour
/// sampling, so the picture stays sharp without the CPU ever touching a magnified copy of it.
///
/// Frame pixels are 0xAARRGGBB with alpha 0xFF. On a platform whose native 32-bit layout is the same (Windows,
/// where Skia is BGRA8888 little-endian) that is a row copy; elsewhere the channels are packed per pixel.
/// </summary>
public sealed class MauiVideoSink : IVideoSink, IDisposable
{
    /// <summary>
    /// True when the platform's native pixel layout is the frame's own 0xAARRGGBB, so rows copy verbatim.
    /// Asked of the package rather than assumed, because the layout differs between Windows and the mobile heads.
    /// </summary>
    private static readonly bool NativeMatchesFrame =
        WriteableBitmap.FromRgba(0x12, 0x34, 0x56, 0xFF) == 0xFF123456u;

    public MauiVideoSink()
    {
        Bitmap = new WriteableBitmap(VicII.VisibleArea.Width, VicII.VisibleArea.Height);
    }

    /// <summary>The pixels, to be handed to a <see cref="WriteableBitmapView"/>'s Source.</summary>
    public WriteableBitmap Bitmap { get; }

    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;

    /// <summary>Frames received so far.</summary>
    public long FramesPresented { get; private set; }

    /// <summary>Called on the emulator thread.</summary>
    public void PresentFrame(in VideoFrame frame)
    {
        FramesPresented++;
        if (frame.Width != Width || frame.Height != Height) return;
        // Non-blocking: a tick is dropped only while the GPU still holds every back buffer.
        if (!Bitmap.TryBeginWrite(out var write)) return;
        try
        {
            var destination = write.Pixels;
            int destinationStride = Bitmap.Stride / sizeof(uint);
            for (int y = 0; y < Height; y++)
            {
                var source = frame.Pixels.AsSpan((frame.VisibleY + y) * frame.FullWidth + frame.VisibleX, Width);
                var row = destination.Slice(y * destinationStride, Width);
                if (NativeMatchesFrame)
                {
                    source.CopyTo(row);
                }
                else
                {
                    for (int x = 0; x < Width; x++)
                    {
                        uint p = source[x];
                        row[x] = WriteableBitmap.FromRgba((byte)(p >> 16), (byte)(p >> 8), (byte)p, 0xFF);
                    }
                }
            }
        }
        finally
        {
            // Publishes the buffer as the newest frame and asks the view for one coalesced redraw.
            write.Dispose();
        }
    }

    public void Dispose() => Bitmap.Dispose();
}
