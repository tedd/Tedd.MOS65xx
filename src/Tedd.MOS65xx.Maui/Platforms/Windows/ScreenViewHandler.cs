using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinUIGrid = Microsoft.UI.Xaml.Controls.Grid;
using WinUIImage = Microsoft.UI.Xaml.Controls.Image;

namespace Tedd.MOS65xx.Maui.Controls;

/// <summary>
/// Windows handler for <see cref="ScreenView"/>: a black panel holding a centred <see cref="WinUIImage"/> whose
/// <see cref="WriteableBitmap"/> is filled straight from the emulator's frame buffer.
///
/// The picture fills the panel, keeping its aspect ratio, exactly as the WPF front-end's Viewbox did. Getting
/// there without the mush that WinUI's smooth resampling makes of C64 pixels takes two steps: the frame is
/// first magnified by a whole number - nearest neighbour, so every pixel stays a hard edged square - to a
/// bitmap at least as large as the area it has to fill, and WinUI then only ever scales that *down* to fit.
/// Downsampling an already magnified picture keeps the edges; upsampling a 384 x 272 one would not.
///
/// The magnification is therefore ceil(fit) rather than floor(fit), and it only changes when the window is
/// resized. <see cref="Microsoft.UI.Xaml.XamlRoot.RasterizationScale"/> is in the arithmetic because the fit
/// has to be worked out in physical pixels, not in the display-scaled units the layout is in.
/// </summary>
public sealed class ScreenViewHandler : ViewHandler<ScreenView, WinUIGrid>
{
    /// <summary>
    /// Magnification ceiling. The bitmap is about as big as the panel, so this only binds on a very large
    /// window; past it WinUI stretches the picture up a little, which is barely visible at that size.
    /// </summary>
    private const int MaxScale = 8;

    public static readonly IPropertyMapper<ScreenView, ScreenViewHandler> ScreenMapper =
        new PropertyMapper<ScreenView, ScreenViewHandler>(ViewMapper)
        {
            [nameof(ScreenView.Sink)] = MapSink,
        };

    private WinUIImage? _image;
    private WriteableBitmap? _bitmap;
    private byte[] _buffer = Array.Empty<byte>();
    private int _scale;
    private bool _rendering;

    public ScreenViewHandler() : base(ScreenMapper)
    {
    }

    private MauiVideoSink? Sink => VirtualView?.Sink;

    protected override WinUIGrid CreatePlatformView()
    {
        _image = new WinUIImage
        {
            // Fill the panel, keep the aspect ratio: the bitmap is built large enough that this only downscales.
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
        };
        var grid = new WinUIGrid
        {
            Background = new WinUIBrush(Windows.UI.Color.FromArgb(0xFF, 0x10, 0x10, 0x10)),
        };
        grid.Children.Add(_image);
        return grid;
    }

    protected override void ConnectHandler(WinUIGrid platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SizeChanged += OnSizeChanged;
        StartRendering();
    }

    protected override void DisconnectHandler(WinUIGrid platformView)
    {
        StopRendering();
        platformView.SizeChanged -= OnSizeChanged;
        _bitmap = null;
        _buffer = Array.Empty<byte>();
        _scale = 0;
        base.DisconnectHandler(platformView);
    }

    private static void MapSink(ScreenViewHandler handler, ScreenView view)
    {
        // A different sink means a differently sized picture: drop the bitmap and let the next tick rebuild it.
        handler._bitmap = null;
        handler._scale = 0;
        if (handler._image is not null) handler._image.Source = null;
    }

    private void StartRendering()
    {
        if (_rendering) return;
        _rendering = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_rendering) return;
        _rendering = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e) => Rebuild();

    private void OnRendering(object? sender, object e)
    {
        var sink = Sink;
        if (sink is null || _image is null) return;
        if (_bitmap is null && !Rebuild()) return;
        if (!sink.CopyScaled(_buffer, _scale)) return;
        Present();
    }

    /// <summary>
    /// (Re)creates the bitmap for the current panel size. Returns false while the panel has no usable size yet.
    /// </summary>
    private bool Rebuild()
    {
        var sink = Sink;
        var grid = PlatformView;
        if (sink is null || _image is null || grid is null) return false;

        double raster = grid.XamlRoot?.RasterizationScale ?? 1.0;
        if (raster <= 0) raster = 1.0;
        double pixelWidth = grid.ActualWidth * raster, pixelHeight = grid.ActualHeight * raster;
        if (pixelWidth < 1 || pixelHeight < 1) return false;

        double fit = Math.Min(pixelWidth / sink.Width, pixelHeight / sink.Height);
        int scale = Math.Clamp((int)Math.Ceiling(fit), 1, MaxScale);
        if (scale == _scale && _bitmap is not null) return true;

        _scale = scale;
        int width = sink.Width * scale, height = sink.Height * scale;
        _bitmap = new WriteableBitmap(width, height);
        _buffer = new byte[width * height * 4];
        _image.Source = _bitmap;
        sink.CopyScaled(_buffer, _scale, force: true);
        Present();
        return true;
    }

    private void Present()
    {
        if (_bitmap is null) return;
        using (var stream = _bitmap.PixelBuffer.AsStream())
            stream.Write(_buffer, 0, _buffer.Length);
        _bitmap.Invalidate();
    }
}
