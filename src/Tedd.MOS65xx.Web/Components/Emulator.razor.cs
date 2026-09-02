using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Tedd.MOS65xx.Emulator.C64;
using Tedd.MOS65xx.Emulator.Tools;
using Tedd.MOS65xx.Hosting;
using Tedd.MOS65xx.Web.Demo;
using Tedd.MOS65xx.Web.Interop;
using Tedd.MOS65xx.Web.Services;

namespace Tedd.MOS65xx.Web.Components;

/// <summary>
/// The interactive emulator: canvas + audio + keyboard/touch input + media and ROM handling.
/// The pieces it glues together are reusable on their own: <see cref="BrowserVideoSink"/>,
/// <see cref="BrowserAudioSink"/>, <see cref="BrowserInput"/>, <see cref="BrowserLoop"/> and <see cref="RomStore"/>.
/// </summary>
public partial class Emulator : ComponentBase, IDisposable
{
    private const string CanvasId = "emu-canvas";
    private const string ScreenId = "emu-screen";
    private const string RootId = "emu-root";
    private const long MaxMediaSize = 8 * 1024 * 1024;
    private const long MaxRomSize = 64 * 1024;

    private enum EmuState { Booting, NeedRoms, Ready, Running, Failed }

    private static readonly (string Label, C64Key Key)[] SoftKeys =
    {
        ("RUN/STOP", C64Key.RunStop), ("RETURN", C64Key.Return), ("SPACE", C64Key.Space),
        ("F1", C64Key.F1), ("F3", C64Key.F3), ("F5", C64Key.F5), ("F7", C64Key.F7),
    };

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private readonly RomStore _roms = new();
    private readonly BrowserInput _input = new();
    private readonly Action<string> _onDroppedFile;
    private EmulatorSession? _session;
    private BrowserVideoSink? _video;
    private BrowserAudioSink? _audio;
    private EmuState _state = EmuState.Booting;
    private string _bootMessage = "";
    private string? _error;
    private string? _message;
    private string? _romMessage;
    private int _sampleRate;
    private double _fps;
    private double _load;
    private bool _basicReady;
    private string _audioState = "off";
    private bool _autostart = true;
    private bool _attachDrive = true;
    private bool _rememberRoms = true;
    private bool _showRoms;
    private bool _touchDevice;
    private bool _typeReturn = true;
    private string _typeText = "";
    private int _joyPort = 2;
    private bool _statusHooked;

    public Emulator()
    {
        _onDroppedFile = OnDroppedFile;
    }

    private bool IsRunning => _state == EmuState.Running && _session is not null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            _bootMessage = "loading scripts";
            await C64Js.ImportAsync(new Uri(new Uri(Nav.BaseUri), "js/c64.js").ToString());
            _video = new BrowserVideoSink(CanvasId);
            _touchDevice = C64Js.IsTouchDevice();
            C64Js.AttachDrop(RootId, _onDroppedFile);
            BrowserLoop.StatusTick += OnStatusTick;
            _statusHooked = true;
            _bootMessage = "looking for ROM images";
            StateHasChanged();
            await LoadRomsAsync();
        }
        catch (Exception ex)
        {
            _state = EmuState.Failed;
            _error = ex.ToString();
        }
        StateHasChanged();
    }

    private async Task LoadRomsAsync()
    {
        bool found = _roms.LoadFromStorage() || await _roms.LoadFromSiteAsync(Http);
        _attachDrive = _roms.HasDrive;
        _state = found ? EmuState.Ready : EmuState.NeedRoms;
        _showRoms = !found;
    }

    #region session lifecycle

    private async Task StartAsync()
    {
        try
        {
            if (!_roms.IsComplete)
            {
                _state = EmuState.NeedRoms;
                return;
            }
            // Must happen in the click handler (browser autoplay policy); returns the real device rate.
            _sampleRate = await C64Js.AudioStart();
            _audioState = C64Js.AudioState();
            if (_session is null || _session.SampleRate != _sampleRate)
                CreateSession();
            _state = EmuState.Running;
            _error = null;
            await BrowserLoop.StartAsync();
            C64Js.FocusElement(ScreenId);
        }
        catch (Exception ex)
        {
            _state = EmuState.Failed;
            _error = ex.ToString();
        }
    }

    /// <summary>(Re)creates the session from the current ROM set and connects the browser surfaces.</summary>
    private void CreateSession()
    {
        if (_session is not null)
        {
            _session.Command -= OnCommand;
            _input.Detach();
        }
        bool drive = _attachDrive && _roms.HasDrive;
        var session = new EmulatorSession(_roms.ToRomSet(drive), _sampleRate, attachDrive: drive);
        _audio = new BrowserAudioSink(_sampleRate);
        session.Audio = _audio;
        session.Video = _video!;
        session.Command += OnCommand;
        _session = session;
        _input.Session = session;
        _input.Attach(ScreenId);
        BrowserLoop.Attach(session, _video!);
    }

    private void OnCommand(SystemCommand command)
    {
        switch (command)
        {
            case SystemCommand.Reset: Reset(); break;
            case SystemCommand.HardReset: _session?.Reset(hard: true); break;
            case SystemCommand.Pause: _ = TogglePauseAsync(); break;
            case SystemCommand.Warp: ToggleWarp(); break;
            case SystemCommand.Screenshot: Screenshot(); break;
        }
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnStatusTick()
    {
        _fps = BrowserLoop.MeasuredFps;
        _load = BrowserLoop.Load;
        _audioState = C64Js.AudioState();
        if (_session is not null)
            _basicReady = _session.Machine.IsBasicReady();
        if (BrowserLoop.Error is { } error && _state == EmuState.Running)
        {
            _state = EmuState.Failed;
            _error = error.ToString();
        }
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        if (_statusHooked) BrowserLoop.StatusTick -= OnStatusTick;
        if (BrowserLoop.Session == _session) BrowserLoop.Detach();
        if (BrowserLoop.IsRunning) BrowserLoop.Stop();
        _input.Detach();
        if (_session is not null) _session.Command -= OnCommand;
    }

    #endregion

    #region toolbar

    private void FocusScreen() => C64Js.FocusElement(ScreenId);

    private void Reset()
    {
        _session?.Reset(hard: false);
        _audio?.Clear();
    }

    private async Task TogglePauseAsync()
    {
        if (_session is null) return;
        _session.Paused = !_session.Paused;
        if (_session.Paused)
        {
            _audio?.Clear();
            await C64Js.AudioSuspend();
        }
        else
        {
            await C64Js.AudioResume();
            BrowserLoop.ResetPacing();
        }
        _audioState = C64Js.AudioState();
    }

    private void ToggleWarp()
    {
        if (_session is null) return;
        _session.Warp = !_session.Warp;
        if (!_session.Warp) BrowserLoop.ResetPacing();
    }

    private void Screenshot() => C64Js.DownloadCanvas(CanvasId, $"c64-{DateTime.Now:yyyyMMdd-HHmmss}.png");

    private void Fullscreen() => C64Js.RequestFullscreen(ScreenId);

    private void RunDemo()
    {
        if (_session is null) return;
        try
        {
            var prg = DemoProgram.BuildPrg();
            if (!_session.Machine.IsBasicReady())
                _session.Reset(hard: false);   // the injection waits for READY.
            _session.AttachProgram(prg, "tech-demo.prg");
            _message = $"Demo assembled ({prg.Length - 2} bytes at ${DemoProgram.LoadAddress:X4}); it starts as soon as BASIC is ready.";
            FocusScreen();
        }
        catch (AssemblerException ex)
        {
            _message = $"Assembler error in line {ex.Line}: {ex.Message}";
        }
    }

    private void Eject()
    {
        if (_session is null) return;
        if (_session.MediaDescription.StartsWith("Cartridge", StringComparison.Ordinal))
            _session.DetachCartridge();
        else
            _session.EjectDisk();
        _message = null;
    }

    private void TypeText()
    {
        if (_session is null || string.IsNullOrEmpty(_typeText)) return;
        _session.TypeText(_typeText.Replace("\r\n", "\n") + (_typeReturn ? "\n" : ""));
        FocusScreen();
    }

    #endregion

    #region touch input

    private void Joy(JoystickInput input, bool pressed) => _session?.SetJoystick(_joyPort, input, pressed);

    private void Key(C64Key key, bool pressed) => _session?.SetKey(key, pressed);

    private void Restore(bool pressed) => _session?.Machine.Keyboard.SetRestore(pressed);

    #endregion

    #region media

    private async Task OnMediaSelected(InputFileChangeEventArgs e)
    {
        try
        {
            var data = await ReadAllAsync(e.File, MaxMediaSize);
            AttachMedia(e.File.Name, data);
        }
        catch (Exception ex)
        {
            _message = "Could not read the file: " + ex.Message;
        }
    }

    private void OnDroppedFile(string name)
    {
        var data = C64Js.TakeDroppedFile(name);
        if (data is null) return;
        if (ClassifyRom(name, data.Length) is { } slot)
        {
            _romMessage = _roms.Accept(slot, name, data);
            _showRoms = true;
            if (_romMessage is null) _message = $"{name} stored as {slot} ROM - press \"Apply & reset\" to use it.";
        }
        else if (IsRunning)
        {
            AttachMedia(name, data);
        }
        else
        {
            _message = "Start the emulator before dropping media files on it.";
        }
        _ = InvokeAsync(StateHasChanged);
    }

    private void AttachMedia(string name, byte[] data)
    {
        if (_session is null) return;
        try
        {
            if (_autostart && !_session.Machine.IsBasicReady())
                _session.Reset(hard: false);   // autostart types into BASIC; make sure we get there
            _session.AttachAuto(data, name, _autostart);
            _message = "Attached " + _session.MediaDescription;
            FocusScreen();
        }
        catch (Exception ex)
        {
            _message = $"Could not attach {name}: {ex.Message}";
        }
    }

    /// <summary>Recognises ROM dumps by size and name so they can be dropped anywhere on the emulator.</summary>
    private static RomSlot? ClassifyRom(string name, int size)
    {
        var n = name.ToLowerInvariant();
        bool romish = n.EndsWith(".bin") || n.EndsWith(".rom") || !n.Contains('.');
        if (!romish) return null;
        if (size == RomSet.CharSize && (n.Contains("char") || n.Contains("901225") || n.Contains("325018"))) return RomSlot.Chargen;
        if (size == RomSet.BasicSize && (n.Contains("basic") || n.Contains("901226"))) return RomSlot.Basic;
        if (size == RomSet.KernalSize && (n.Contains("kernal") || n.Contains("kernel") || n.Contains("901227"))) return RomSlot.Kernal;
        if (size is 8192 or 16384 && (n.Contains("1541") || n.Contains("1540") || n.Contains("dos") || n.Contains("325302") || n.Contains("901229"))) return RomSlot.Drive;
        if (size == 16384 && (n.Contains("64c") || n.Contains("251913") || n.Contains("basic+kernal"))) return RomSlot.Combined;
        return null;
    }

    #endregion

    #region roms

    private async Task OnRomSelected(InputFileChangeEventArgs e, RomSlot slot)
    {
        _romMessage = null;
        try
        {
            foreach (var file in e.GetMultipleFiles(4))
            {
                var data = await ReadAllAsync(file, MaxRomSize);
                var err = _roms.Accept(slot, file.Name, data);
                if (err is not null) { _romMessage = err; break; }
            }
            if (_roms.HasDrive) _attachDrive = true;
            if (_roms.IsComplete && _state == EmuState.NeedRoms) _state = EmuState.Ready;
            if (_roms.IsComplete && _rememberRoms && !_roms.SaveToStorage())
                _romMessage = "The ROMs are loaded but could not be stored (localStorage unavailable or full).";
        }
        catch (Exception ex)
        {
            _romMessage = ex.Message;
        }
    }

    private async Task ApplyRomsAsync()
    {
        if (!_roms.IsComplete) return;
        _romMessage = null;
        if (_rememberRoms && !_roms.SaveToStorage())
            _romMessage = "Applied, but the ROMs could not be stored in localStorage.";
        if (_state == EmuState.Running)
        {
            CreateSession();
            BrowserLoop.ResetPacing();
        }
        else
        {
            _state = EmuState.Ready;
            if (_session is not null) { _session.Command -= OnCommand; _session = null; }
        }
        await Task.CompletedTask;
    }

    private async Task ForgetRomsAsync()
    {
        RomStore.ClearStorage();
        _romMessage = "Stored ROMs removed; reloading the site's ROM set.";
        var fresh = new RomStore();
        if (await fresh.LoadFromSiteAsync(Http))
        {
            foreach (var slot in new[] { RomSlot.Basic, RomSlot.Kernal, RomSlot.Chargen })
                _roms.Accept(slot, slot switch { RomSlot.Basic => fresh.BasicName, RomSlot.Kernal => fresh.KernalName, _ => fresh.ChargenName },
                    slot switch { RomSlot.Basic => fresh.Basic!, RomSlot.Kernal => fresh.Kernal!, _ => fresh.Chargen! });
            if (fresh.Drive is not null) _roms.Accept(RomSlot.Drive, fresh.DriveName, fresh.Drive); else _roms.RemoveDrive();
            _attachDrive = _roms.HasDrive;
        }
        else
        {
            _romMessage = "Stored ROMs removed; the site has no ROM images, load your own.";
        }
    }

    private static async Task<byte[]> ReadAllAsync(IBrowserFile file, long maxSize)
    {
        await using var stream = file.OpenReadStream(maxSize);
        using var ms = new MemoryStream((int)Math.Min(file.Size, int.MaxValue));
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    #endregion
}
