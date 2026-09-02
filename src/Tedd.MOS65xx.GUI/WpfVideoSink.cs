using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tedd.MOS65xx.Emulator.Video;
using Tedd.MOS65xx.Hosting;

namespace Tedd.MOS65xx.GUI;

/// <summary>
/// <see cref="IVideoSink"/> for WPF: the emulator thread copies the visible 384 x 272 picture as BGRA bytes into
/// a buffer; the UI thread blits it into a <see cref="WriteableBitmap"/> from <c>CompositionTarget.Rendering</c>.
/// </summary>
public sealed class WpfVideoSink : IVideoSink
{
    private readonly byte[] _pixels;
    private readonly object _lock = new();
    private volatile bool _dirty;

    public WpfVideoSink()
    {
        Width = VicII.VisibleArea.Width;
        Height = VicII.VisibleArea.Height;
        _pixels = new byte[Width * Height * 4];
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Frames received so far.</summary>
    public long FramesPresented { get; private set; }

    /// <summary>Creates a bitmap of the right size and format for <see cref="Blit"/>.</summary>
    public WriteableBitmap CreateBitmap() => new(Width, Height, 96, 96, PixelFormats.Bgra32, null);

    /// <summary>Called on the emulator thread.</summary>
    public void PresentFrame(in VideoFrame frame)
    {
        lock (_lock)
        {
            frame.CopyVisibleBgra(_pixels);
            _dirty = true;
            FramesPresented++;
        }
    }

    /// <summary>
    /// Copies the latest frame into the bitmap if a new one arrived since the last call. Must be called on the
    /// thread that owns the bitmap. Returns true when the bitmap was updated.
    /// </summary>
    public bool Blit(WriteableBitmap bitmap)
    {
        if (!_dirty) return false;
        bitmap.Lock();
        try
        {
            lock (_lock)
            {
                int rowBytes = Width * 4;
                int stride = bitmap.BackBufferStride;
                if (stride == rowBytes)
                {
                    Marshal.Copy(_pixels, 0, bitmap.BackBuffer, _pixels.Length);
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
