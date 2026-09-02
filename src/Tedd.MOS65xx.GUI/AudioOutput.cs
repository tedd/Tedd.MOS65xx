using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Tedd.MOS65xx.GUI;

/// <summary>Plays 16-bit mono PCM produced by the SID resampler through the default output device.</summary>
public sealed class AudioOutput : IDisposable
{
    private readonly WasapiOut _out;
    private readonly BufferedWaveProvider _buffer;
    private readonly byte[] _bytes = new byte[16384];

    public AudioOutput(int sampleRate)
    {
        _buffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 16, 1), TimeSpan.FromMilliseconds(500))
        {
            DiscardOnBufferOverflow = true,
        };
        _out = new WasapiOut(AudioClientShareMode.Shared, 60);
        _out.Init(_buffer);
        _out.Play();
    }

    /// <summary>Queues samples for playback.</summary>
    public void Write(short[] samples, int count)
    {
        int bytes = count * 2;
        if (bytes > _bytes.Length) bytes = _bytes.Length;
        Buffer.BlockCopy(samples, 0, _bytes, 0, bytes);
        _buffer.AddSamples(_bytes, 0, bytes);
    }

    /// <summary>Drops queued audio (used in warp mode).</summary>
    public void Clear() => _buffer.ClearBuffer();

    public void Dispose()
    {
        _out.Stop();
        _out.Dispose();
    }
}
