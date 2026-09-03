using Tedd.MOS65xx.Emulator.Video;
using Tedd.MOS65xx.Hosting;

namespace Tedd.MOS65xx.Maui.Controls;

/// <summary>
/// <see cref="IVideoSink"/> for MAUI: the emulator thread copies the visible 384 x 272 picture as BGRA bytes
/// into a buffer, and the UI thread pulls the latest one out with <see cref="CopyScaled"/> whenever the
/// platform is about to compose a frame. The scaling happens on the way out (nearest neighbour, integer
/// factors only) so the emulator thread stays cheap and the picture stays sharp.
///
/// Three buffers rotate between the two threads - one being written, one waiting, one being read - so the lock
/// is only ever held for a handful of reference swaps. Sharing one buffer would make the emulator thread wait
/// for the (much larger) magnified copy, which costs real frames at a large window size.
/// </summary>
public sealed class MauiVideoSink : IVideoSink
{
    private readonly object _lock = new();
    private byte[] _write;      // the emulator thread's buffer
    private byte[]? _ready;     // a finished frame the UI thread has not taken yet
    private byte[]? _spare;     // the third buffer, whenever it is not the ready one
    private byte[] _read;       // the UI thread's buffer

    public MauiVideoSink()
    {
        Width = VicII.VisibleArea.Width;
        Height = VicII.VisibleArea.Height;
        int size = Width * Height * 4;
        _write = new byte[size];
        _read = new byte[size];
        _spare = new byte[size];
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Frames received so far.</summary>
    public long FramesPresented { get; private set; }

    /// <summary>Called on the emulator thread.</summary>
    public void PresentFrame(in VideoFrame frame)
    {
        frame.CopyVisibleBgra(_write);
        lock (_lock)
        {
            if (_ready is not null)
            {
                // The UI thread never took the previous frame: drop it and write into that buffer next.
                (_write, _ready) = (_ready, _write);
            }
            else
            {
                _ready = _write;
                _write = _spare!;
                _spare = null;
            }
            FramesPresented++;
        }
    }

    /// <summary>
    /// Copies the latest frame into <paramref name="destination"/> (BGRA, <c>Width * scale</c> pixels per row),
    /// magnified <paramref name="scale"/> times. Returns false, leaving the destination alone, when no new frame
    /// arrived since the last call and <paramref name="force"/> is false.
    /// </summary>
    public bool CopyScaled(Span<byte> destination, int scale, bool force = false)
    {
        if (scale < 1) scale = 1;
        int rowBytes = Width * scale * 4;
        if (destination.Length < rowBytes * Height * scale)
            throw new ArgumentException("Destination too small", nameof(destination));

        lock (_lock)
        {
            if (_ready is null)
            {
                if (!force) return false;
            }
            else
            {
                // Take the finished frame and hand the buffer we were reading back to the emulator thread.
                _spare = _read;
                _read = _ready;
                _ready = null;
            }
        }

        var source = _read;
        for (int y = 0; y < Height; y++)
        {
            // Expand one source row into the first of its destination rows...
            var row = destination.Slice(y * scale * rowBytes, rowBytes);
            int at = y * Width * 4;
            int o = 0;
            for (int x = 0; x < Width; x++)
            {
                byte b = source[at], g = source[at + 1], r = source[at + 2], a = source[at + 3];
                at += 4;
                for (int n = 0; n < scale; n++)
                {
                    row[o] = b;
                    row[o + 1] = g;
                    row[o + 2] = r;
                    row[o + 3] = a;
                    o += 4;
                }
            }
            // ...then repeat it for the rest of them.
            for (int n = 1; n < scale; n++)
                row.CopyTo(destination.Slice((y * scale + n) * rowBytes, rowBytes));
        }
        return true;
    }
}
