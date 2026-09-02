using System.Runtime.Versioning;
using Tedd.MOS65xx.Emulator.Video;
using Tedd.MOS65xx.Hosting;

namespace Tedd.MOS65xx.Web.Interop;

/// <summary>
/// <see cref="IVideoSink"/> that draws on an HTML canvas through <c>c64.js</c>.
/// <see cref="PresentFrame"/> only remembers the frame (the VIC buffer stays valid until the next
/// <c>RunFrame</c>); <see cref="Flush"/> converts the visible 384x272 area to RGBA and pushes it with
/// <c>putImageData</c>. The host calls <see cref="Flush"/> once per animation frame, so when several
/// emulated frames run in one tick only the last one is drawn.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserVideoSink : IVideoSink
{
    public static readonly int Width = VicII.VisibleArea.Width;
    public static readonly int Height = VicII.VisibleArea.Height;

    private readonly byte[] _rgba = new byte[Width * Height * 4];
    private uint[]? _pending;
    private long _pendingFrame;

    /// <param name="canvasId">DOM id of the canvas; it is resized to 384x272 device pixels (CSS does the scaling).</param>
    public BrowserVideoSink(string canvasId)
    {
        CanvasId = canvasId;
        C64Js.AttachCanvas(canvasId, Width, Height);
    }

    public string CanvasId { get; }

    /// <summary>Number of frames actually drawn.</summary>
    public long PresentedFrames { get; private set; }

    public void PresentFrame(in VideoFrame frame)
    {
        _pending = frame.Pixels;
        _pendingFrame = frame.FrameNumber;
    }

    /// <summary>Draws the most recently presented frame, if any new one arrived since the last flush.</summary>
    public void Flush()
    {
        var pixels = _pending;
        if (pixels is null) return;
        _pending = null;
        new VideoFrame(pixels, _pendingFrame).CopyVisibleRgba(_rgba);
        C64Js.PresentFrame(_rgba);
        PresentedFrames++;
    }
}
