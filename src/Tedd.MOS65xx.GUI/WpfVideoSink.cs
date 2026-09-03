using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tedd.MOS65xx.Emulator.Video;
using Tedd.MOS65xx.Hosting;

namespace Tedd.MOS65xx.GUI;

/// <summary>
/// <see cref="IVideoSink"/> for WPF: the emulator thread copies the visible picture as BGRA bytes into a buffer;
/// the UI thread blits it into a <see cref="WriteableBitmap"/> from <c>CompositionTarget.Rendering</c>. The picture
/// is 384 x 272 for the VIC-II and 768 x 272 for the C128's VDC; when the size changes <see cref="Blit"/> returns
/// false with <see cref="SizeChanged"/> set, and the window creates a new bitmap with <see cref="CreateBitmap"/>.
/// </summary>
public sealed class WpfVideoSink : IVideoSink
{
    private byte[] _pixels;
    private readonly object _lock = new();
    private volatile bool _dirty;
    private int _pendingWidth, _pendingHeight;

    public WpfVideoSink()
    {
        Width = _pendingWidth = VicII.VisibleArea.Width;
        Height = _pendingHeight = VicII.VisibleArea.Height;
        _pixels = new byte[Vdc8563.FrameWidth * Vdc8563.FrameHeight * 4];
    }

    /// <summary>Width of the picture the current bitmap must have.</summary>
    public int Width { get; private set; }
    /// <summary>Height of the picture the current bitmap must have.</summary>
    public int Height { get; private set; }

    /// <summary>True when the last presented frame has another size than the bitmap; call <see cref="CreateBitmap"/> again.</summary>
    public bool SizeChanged => _pendingWidth != Width || _pendingHeight != Height;

    /// <summary>Frames received so far.</summary>
    public long FramesPresented { get; private set; }

    /// <summary>Creates a bitmap of the right size and format for <see cref="Blit"/> (and adopts that size).</summary>
    public WriteableBitmap CreateBitmap()
    {
        lock (_lock)
        {
            Width = _pendingWidth;
            Height = _pendingHeight;
        }
        return new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
    }

    /// <summary>Called on the emulator thread.</summary>
    public void PresentFrame(in VideoFrame frame)
    {
        lock (_lock)
        {
            if (_pixels.Length < frame.Width * frame.Height * 4)
                _pixels = new byte[frame.Width * frame.Height * 4];
            frame.CopyVisibleBgra(_pixels);
            _pendingWidth = frame.Width;
            _pendingHeight = frame.Height;
            _dirty = true;
            FramesPresented++;
        }
    }

    /// <summary>
    /// Copies the latest frame into the bitmap if a new one arrived since the last call. Must be called on the
    /// thread that owns the bitmap. Returns true when the bitmap was updated; false when nothing new arrived or the
    /// picture size changed (<see cref="SizeChanged"/>).
    /// </summary>
    public bool Blit(WriteableBitmap bitmap)
    {
        if (!_dirty) return false;
        lock (_lock)
        {
            if (SizeChanged) return false;
        }
        bitmap.Lock();
        try
        {
            lock (_lock)
            {
                int rowBytes = Width * 4;
                int stride = bitmap.BackBufferStride;
                if (stride == rowBytes)
                {
                    Marshal.Copy(_pixels, 0, bitmap.BackBuffer, rowBytes * Height);
                }
                else
                {
                    for (int y = 0; y < Height; y++)
                        Marshal.Copy(_pixels, y * rowBytes, bitmap.BackBuffer + y * stride, rowBytes);
                }
                _dirty = false;
            }
            bitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
        }
        finally
        {
            bitmap.Unlock();
        }
        return true;
    }
}
