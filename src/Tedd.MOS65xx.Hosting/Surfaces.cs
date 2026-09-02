using System;
using Tedd.MOS65xx.Emulator.Video;

namespace Tedd.MOS65xx.Hosting;

/// <summary>
/// One rendered PAL frame. The pixel buffer is owned by the emulator and reused for the next frame, so sinks
/// must copy what they need before returning from <see cref="IVideoSink.PresentFrame"/>.
/// </summary>
public readonly struct VideoFrame
{
    public VideoFrame(uint[] pixels, long frameNumber)
    {
        Pixels = pixels;
        FrameNumber = frameNumber;
    }

    /// <summary>Full raster (504 x 312) ARGB pixels, 0xAARRGGBB with alpha = 0xFF.</summary>
    public uint[] Pixels { get; }
    /// <summary>Frame counter since power-on.</summary>
    public long FrameNumber { get; }
    /// <summary>Width of the full raster buffer.</summary>
    public int FullWidth => VicII.FrameWidth;
    /// <summary>Height of the full raster buffer.</summary>
    public int FullHeight => VicII.FrameHeight;
    /// <summary>Width of the standard visible PAL picture (384).</summary>
    public int Width => VicII.VisibleArea.Width;
    /// <summary>Height of the standard visible PAL picture (272).</summary>
    public int Height => VicII.VisibleArea.Height;

    /// <summary>Copies the visible picture (Width x Height, top-left first) into <paramref name="destination"/>.</summary>
    public void CopyVisible(Span<uint> destination)
    {
        var vis = VicII.VisibleArea;
        if (destination.Length < vis.Width * vis.Height)
            throw new ArgumentException("Destination too small", nameof(destination));
        for (int y = 0; y < vis.Height; y++)
            Pixels.AsSpan((vis.Y + y) * VicII.FrameWidth + vis.X, vis.Width).CopyTo(destination.Slice(y * vis.Width, vis.Width));
    }

    /// <summary>Copies the visible picture as RGBA bytes (R, G, B, A order, as used by browser canvases and most GPU textures).</summary>
    public void CopyVisibleRgba(Span<byte> destination)
    {
        var vis = VicII.VisibleArea;
        if (destination.Length < vis.Width * vis.Height * 4)
            throw new ArgumentException("Destination too small", nameof(destination));
        int o = 0;
        for (int y = 0; y < vis.Height; y++)
        {
            int src = (vis.Y + y) * VicII.FrameWidth + vis.X;
            for (int x = 0; x < vis.Width; x++)
            {
                uint p = Pixels[src + x];
                destination[o++] = (byte)(p >> 16);
                destination[o++] = (byte)(p >> 8);
                destination[o++] = (byte)p;
                destination[o++] = 0xFF;
            }
        }
    }

    /// <summary>Copies the visible picture as BGRA bytes (little-endian ARGB, as used by WPF/Direct3D/SDL ARGB8888).</summary>
    public void CopyVisibleBgra(Span<byte> destination)
    {
        var vis = VicII.VisibleArea;
        if (destination.Length < vis.Width * vis.Height * 4)
            throw new ArgumentException("Destination too small", nameof(destination));
        int o = 0;
        for (int y = 0; y < vis.Height; y++)
        {
            int src = (vis.Y + y) * VicII.FrameWidth + vis.X;
            for (int x = 0; x < vis.Width; x++)
            {
                uint p = Pixels[src + x];
                destination[o++] = (byte)p;
                destination[o++] = (byte)(p >> 8);
                destination[o++] = (byte)(p >> 16);
                destination[o++] = 0xFF;
            }
        }
    }
}

/// <summary>Receives every rendered frame. Called on the thread that runs the emulation.</summary>
public interface IVideoSink
{
    void PresentFrame(in VideoFrame frame);
}

/// <summary>Receives 16-bit mono PCM produced by the SID. Called on the thread that runs the emulation.</summary>
public interface IAudioSink
{
    /// <summary>Sample rate of the samples that will be written (fixed for the lifetime of the session).</summary>
    int SampleRate { get; }
    void Write(ReadOnlySpan<short> samples);
    /// <summary>Drops queued audio (used when entering warp mode or after a pause).</summary>
    void Clear();
}

/// <summary>A video sink that does nothing (headless use).</summary>
public sealed class NullVideoSink : IVideoSink
{
    public static readonly NullVideoSink Instance = new();
    public void PresentFrame(in VideoFrame frame) { }
}

/// <summary>An audio sink that discards everything.</summary>
public sealed class NullAudioSink : IAudioSink
{
    public NullAudioSink(int sampleRate = 44100) => SampleRate = sampleRate;
    public int SampleRate { get; }
    public void Write(ReadOnlySpan<short> samples) { }
    public void Clear() { }
}

/// <summary>
/// An audio sink decorator that keeps the most recent samples in a ring buffer (for visualizers) and forwards
/// everything to an optional inner sink.
/// </summary>
public sealed class AudioTap : IAudioSink
{
    private readonly IAudioSink? _inner;
    private readonly short[] _ring;
    private int _write;
    private long _total;
    private readonly object _lock = new();

    public AudioTap(IAudioSink? inner, int sampleRate, int capacity = 16384)
    {
        _inner = inner;
        SampleRate = inner?.SampleRate ?? sampleRate;
        _ring = new short[capacity];
    }

    public int SampleRate { get; }
    public int Capacity => _ring.Length;
    /// <summary>Total number of samples that passed through.</summary>
    public long TotalSamples => _total;

    public void Write(ReadOnlySpan<short> samples)
    {
        lock (_lock)
        {
            foreach (var s in samples)
            {
                _ring[_write] = s;
                _write = (_write + 1) % _ring.Length;
            }
            _total += samples.Length;
        }
        _inner?.Write(samples);
    }

    public void Clear() => _inner?.Clear();

    /// <summary>Copies the latest <c>destination.Length</c> samples (oldest first) into the destination.</summary>
    public void CopyLatest(Span<short> destination)
    {
        lock (_lock)
        {
            int n = Math.Min(destination.Length, _ring.Length);
            int start = (_write - n + _ring.Length) % _ring.Length;
            for (int i = 0; i < n; i++)
                destination[destination.Length - n + i] = _ring[(start + i) % _ring.Length];
            if (n < destination.Length)
                destination[..(destination.Length - n)].Clear();
        }
    }
}
