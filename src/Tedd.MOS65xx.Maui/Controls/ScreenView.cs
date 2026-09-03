namespace Tedd.MOS65xx.Maui.Controls;

/// <summary>
/// The emulator picture. The view itself only carries the <see cref="MauiVideoSink"/> the frames arrive in;
/// its handler owns a native surface, drives its own render loop and blits the newest frame at the largest
/// whole-number magnification that fits, so C64 pixels stay square and hard edged.
/// </summary>
public sealed class ScreenView : View
{
    public static readonly BindableProperty SinkProperty =
        BindableProperty.Create(nameof(Sink), typeof(MauiVideoSink), typeof(ScreenView));

    /// <summary>Where the emulator puts its frames; null shows an empty (black) screen.</summary>
    public MauiVideoSink? Sink
    {
        get => (MauiVideoSink?)GetValue(SinkProperty);
        set => SetValue(SinkProperty, value);
    }
}
