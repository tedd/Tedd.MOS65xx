using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Tedd.MOS65xx.Emulator.C64;

namespace Tedd.MOS65xx.GUI;

/// <summary>
/// Runs a <see cref="C64"/> on its own thread at real-time speed, publishes frames and audio, and provides safe
/// access to the machine from the UI thread (everything touching the machine goes through <see cref="Invoke"/>
/// or happens while the machine is frozen).
/// </summary>
public sealed class EmulatorHost : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _resume = new(true);
    private readonly ConcurrentQueue<Action> _actions = new();
    private readonly object _sync = new();
    private volatile bool _running = true;
    private volatile bool _paused;
    private readonly uint[] _frameCopy;
    private readonly short[] _audioBuffer = new short[8192];
    private readonly AudioOutput? _audio;
    private double _measuredFps;

    public C64 Machine { get; }

    /// <summary>Raised on the emulator thread after every frame with a copy of the VIC frame buffer.</summary>
    public event Action<uint[]>? FrameRendered;

    /// <summary>Run as fast as possible (no pacing, audio muted).</summary>
    public volatile bool Warp;

    /// <summary>Frames per second measured over the last second.</summary>
    public double MeasuredFps => _measuredFps;

    public EmulatorHost(C64 machine, bool enableAudio)
    {
        Machine = machine;
        _frameCopy = new uint[machine.Vic.Frame.Length];
        if (enableAudio)
        {
            try { _audio = new AudioOutput(44100); }
            catch (Exception ex) { Debug.WriteLine("Audio disabled: " + ex.Message); }
        }
        _thread = new Thread(Run) { IsBackground = true, Name = "C64 emulation" };
    }

    public void Start() => _thread.Start();

    /// <summary>true = the machine is frozen (no cycles executed); memory can be inspected safely.</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            if (_paused == value) return;
            _paused = value;
            if (value) _resume.Reset(); else _resume.Set();
        }
    }

    /// <summary>Runs an action on the emulator thread between two frames and waits for it (also works while paused).</summary>
    public void Invoke(Action action)
    {
        if (Thread.CurrentThread == _thread)
        {
            action();
            return;
        }
        // Frozen machine: the emulator thread is blocked, so run the action directly under the lock.
        lock (_sync)
        {
            action();
        }
    }

    public T Invoke<T>(Func<T> func)
    {
        T result = default!;
        Invoke(() => result = func());
        return result;
    }

    private void Run()
    {
        var clock = Stopwatch.StartNew();
        double nextFrame = 0;
        double frameSeconds = 1.0 / C64.FrameRate;
        int fpsFrames = 0;
        double fpsStart = 0;

        while (_running)
        {
            if (_paused)
            {
                _resume.Wait(50);
                if (_paused)
                {
                    clock.Restart();
                    nextFrame = 0;
                    fpsStart = 0;
                    fpsFrames = 0;
                    continue;
                }
            }

            lock (_sync)
            {
                Machine.RunFrame();
                Array.Copy(Machine.Vic.Frame, _frameCopy, _frameCopy.Length);
                int n = Machine.Audio.Read(_audioBuffer);
                if (_audio is not null)
                {
                    if (Warp) _audio.Clear();
                    else _audio.Write(_audioBuffer, n);
                }
            }
            FrameRendered?.Invoke(_frameCopy);

            fpsFrames++;
            double now = clock.Elapsed.TotalSeconds;
            if (now - fpsStart >= 1.0)
            {
                _measuredFps = fpsFrames / (now - fpsStart);
                fpsFrames = 0;
                fpsStart = now;
            }

            if (Warp)
            {
                nextFrame = now;
                continue;
            }

            // Pace to the PAL frame rate; if we fell far behind (e.g. after a pause) resynchronise instead of racing.
            nextFrame += frameSeconds;
            double ahead = nextFrame - clock.Elapsed.TotalSeconds;
            if (ahead > 0.002)
                Thread.Sleep((int)((ahead - 0.001) * 1000));
            while (clock.Elapsed.TotalSeconds < nextFrame)
                Thread.SpinWait(50);
            if (clock.Elapsed.TotalSeconds - nextFrame > 0.25)
                nextFrame = clock.Elapsed.TotalSeconds;
        }
    }

    public void Dispose()
    {
        _running = false;
        _paused = false;
        _resume.Set();
        if (_thread.IsAlive && Thread.CurrentThread != _thread)
            _thread.Join(1000);
        _audio?.Dispose();
    }
}
