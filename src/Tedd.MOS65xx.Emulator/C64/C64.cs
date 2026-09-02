using System;
using System.Collections.Generic;
using System.Text;
using Tedd.MOS65xx.Emulator.Audio;
using Tedd.MOS65xx.Emulator.Cpu;
using Tedd.MOS65xx.Emulator.Drive;
using Tedd.MOS65xx.Emulator.IO;
using Tedd.MOS65xx.Emulator.Media;
using Tedd.MOS65xx.Emulator.Video;

namespace Tedd.MOS65xx.Emulator.C64;

/// <summary>
/// A complete PAL Commodore 64: 6510 CPU, PLA/memory, VIC-II 6569, two CIA 6526, SID 6581, keyboard, two
/// joysticks, expansion port (cartridge), the IEC serial bus and optionally a 1541 disk drive.
/// Everything advances one system cycle per <see cref="Clock"/> call; see docs/ARCHITECTURE.md ("Machine
/// timing model") for the ordering.
/// </summary>
public sealed class C64
{
    /// <summary>PAL system clock in Hz.</summary>
    public const double ClockFrequency = 985248.0;
    /// <summary>Cycles per PAL frame (63 cycles x 312 lines).</summary>
    public const int CyclesPerFrame = VicII.CyclesPerLine * VicII.LinesPerFrame;
    /// <summary>PAL frame rate.</summary>
    public const double FrameRate = ClockFrequency / CyclesPerFrame;
    /// <summary>The 1541's own clock in Hz.</summary>
    public const double DriveClockFrequency = 1_000_000.0;

    private const int ClockInt = 985248;
    private const int DriveClockInt = 1_000_000;
    private const int TodHz = 50;

    public RomSet Roms { get; }
    public C64Memory Memory { get; }
    public Cpu6502 Cpu { get; }
    public VicII Vic { get; }
    public Cia6526 Cia1 { get; }
    public Cia6526 Cia2 { get; }
    public Sid6581 Sid { get; }
    public SidResampler Audio { get; }
    public Keyboard Keyboard { get; } = new();
    public Joystick Joystick1 { get; } = new();
    public Joystick Joystick2 { get; } = new();
    public IecBus Iec { get; } = new();
    public IecPort IecPort { get; }
    public Drive1541? Drive { get; private set; }

    /// <summary>System cycles executed since power-on.</summary>
    public long Cycles { get; private set; }
    /// <summary>Frames completed since power-on.</summary>
    public long Frames { get; private set; }

    private int _todAccumulator;
    private int _driveAccumulator;
    private bool _frameDone;
    private readonly Queue<byte> _typeAhead = new();
    private Action? _injectWhenReady;

    public C64(RomSet roms, int audioSampleRate = 44100)
    {
        Roms = roms;
        Memory = new C64Memory(roms);
        Cpu = new Cpu6502(Memory);
        Vic = new VicII(Memory);
        Cia1 = new Cia6526("CIA1");
        Cia2 = new Cia6526("CIA2");
        Sid = new Sid6581();
        Audio = new SidResampler(Sid, audioSampleRate, ClockFrequency);
        Memory.Vic = Vic;
        Memory.Sid = Sid;
        Memory.Cia1 = Cia1;
        Memory.Cia2 = Cia2;
        IecPort = Iec.Attach("C64");

        // CIA1: port A drives the keyboard rows (and reads joystick 2), port B reads the columns (and joystick 1).
        // The matrix is passive, so both directions are computed (Programmer's Reference Guide, keyboard matrix).
        Cia1.PortAInput = () => (byte)(Keyboard.ReadRows((byte)(Cia1.PortBOutput & Joystick1.Levels)) & Joystick2.Levels);
        Cia1.PortBInput = () => (byte)(Keyboard.ReadColumns((byte)(Cia1.PortAOutput & Joystick2.Levels)) & Joystick1.Levels);

        // CIA2: PA0-1 VIC bank (inverted), PA3 ATN out, PA4 CLK out, PA5 DATA out, PA6 CLK in, PA7 DATA in.
        Cia2.PortAChanged += UpdateCia2PortA;
        Cia2.PortAInput = () => (byte)(0x3F | (Iec.ClkLow ? 0 : 0x40) | (Iec.DataLow ? 0 : 0x80));
        Cia2.PortBInput = () => 0xFF; // user port, nothing connected

        Vic.FrameCompleted += () => _frameDone = true;

        Reset(hard: true);
    }

    private void UpdateCia2PortA()
    {
        byte pa = Cia2.PortAOutput;
        Memory.VicBank = ~pa & 3;
        IecPort.Set(pullAtn: (pa & 0x08) != 0, pullClk: (pa & 0x10) != 0, pullData: (pa & 0x20) != 0);
    }

    /// <summary>Resets the machine. A hard reset (power cycle) also clears RAM to the power-on pattern.</summary>
    public void Reset(bool hard = false)
    {
        Memory.Reset(hard);
        Vic.Reset();
        Cia1.Reset();
        Cia2.Reset();
        Sid.Reset();
        Cpu.Reset();
        Drive?.Reset();
        Keyboard.ReleaseAll();
        _typeAhead.Clear();
        _injectWhenReady = null;
        Autostart = AutostartState.Idle;
        UpdateCia2PortA();
    }

    /// <summary>Advances the whole machine by one system cycle.</summary>
    public void Clock()
    {
        Vic.Clock();
        Cia1.Clock();
        Cia2.Clock();
        Cpu.Rdy = !Vic.Ba;
        Cpu.Irq = Vic.Irq || Cia1.IrqLine;
        Cpu.Nmi = Cia2.IrqLine || Keyboard.RestorePressed;
        Cpu.Clock();
        Audio.Clock();

        _todAccumulator += TodHz;
        if (_todAccumulator >= ClockInt)
        {
            _todAccumulator -= ClockInt;
            Cia1.TodTick();
            Cia2.TodTick();
        }

        if (Drive is { } drive)
        {
            _driveAccumulator += DriveClockInt;
            while (_driveAccumulator >= ClockInt)
            {
                _driveAccumulator -= ClockInt;
                drive.Clock();
            }
        }

        Cycles++;
    }

    /// <summary>Runs until the VIC completes the current frame. Returns the number of cycles executed.</summary>
    public int RunFrame()
    {
        _frameDone = false;
        int n = 0;
        while (!_frameDone)
        {
            Clock();
            n++;
        }
        Frames++;
        ServiceTypeAhead();
        ServiceAutostart();
        return n;
    }

    /// <summary>Runs the given number of cycles.</summary>
    public void RunCycles(long cycles)
    {
        for (long i = 0; i < cycles; i++)
            Clock();
    }

    /// <summary>Runs whole instructions until the CPU is at an instruction boundary again (at least one cycle).</summary>
    public int StepInstruction()
    {
        int n = 0;
        do
        {
            Clock();
            n++;
        } while (!Cpu.AtInstructionBoundary && n < 100_000);
        return n;
    }

    #region Peripherals

    /// <summary>Plugs a cartridge into the expansion port (null removes it). A reset is needed to start it.</summary>
    public void AttachCartridge(Cartridge? cartridge) => Memory.Cartridge = cartridge;

    /// <summary>
    /// Connects a 1541 drive (device 8 by default) using the ROM from the ROM set. Returns the drive.
    /// Throws if the ROM set has no 1541 ROM.
    /// </summary>
    public Drive1541 AttachDrive(int deviceNumber = 8)
    {
        if (Roms.Drive1541 is null)
            throw new InvalidOperationException("No 1541 ROM available in the ROM set");
        Drive?.Detach();
        Drive = new Drive1541(Roms.Drive1541, Iec, deviceNumber);
        Drive.Reset();
        return Drive;
    }

    public void DetachDrive()
    {
        Drive?.Detach();
        Drive = null;
    }

    #endregion

    #region Screen helpers

    /// <summary>Address of the video matrix (screen RAM) in CPU address space, from the VIC bank and $D018.</summary>
    public int ScreenAddress => (Memory.VicBank << 14) | ((Vic.Peek(0x18) & 0xF0) << 6);

    /// <summary>Returns the 25 lines of 40 characters currently in screen memory, converted from screen codes to ASCII.</summary>
    public string GetScreenText()
    {
        int baseAddr = ScreenAddress;
        var sb = new StringBuilder(25 * 41);
        var line = new char[40];
        for (int row = 0; row < 25; row++)
        {
            int len = 0;
            for (int col = 0; col < 40; col++)
            {
                // Reverse-video bit ignored; trailing spaces trimmed so callers can match line by line.
                char c = Petscii.ScreenCodeToAscii((byte)(Memory.Ram[(baseAddr + row * 40 + col) & 0xFFFF] & 0x7F));
                line[col] = c;
                if (c != ' ') len = col + 1;
            }
            sb.Append(line, 0, len).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>True when the BASIC "READY." prompt is on screen and the KERNAL is waiting for input.</summary>
    public bool IsBasicReady()
    {
        return GetScreenText().Contains("READY.", StringComparison.Ordinal) && Memory.Ram[0xC6] == 0;
    }

    /// <summary>Runs frames until BASIC is ready or the frame budget is exhausted. Returns true when ready.</summary>
    public bool WaitForBasicReady(int maxFrames = 300)
    {
        for (int i = 0; i < maxFrames; i++)
        {
            RunFrame();
            if (IsBasicReady()) return true;
        }
        return IsBasicReady();
    }

    #endregion

    #region Typing and program injection

    /// <summary>
    /// Types text as if entered on the keyboard, using the KERNAL keyboard buffer ($0277, count in $C6).
    /// The text is queued and fed a few characters per frame; '\n' produces RETURN. Uppercase/lowercase are
    /// mapped to PETSCII so that BASIC commands can be typed in either case.
    /// </summary>
    public void TypeText(string text)
    {
        foreach (var b in Petscii.AsciiToKeyboardBuffer(text))
            _typeAhead.Enqueue(b);
        ServiceTypeAhead();
    }

    /// <summary>Number of characters still waiting to be typed.</summary>
    public int PendingTypeAhead => _typeAhead.Count;

    private void ServiceTypeAhead()
    {
        if (_injectWhenReady is not null && IsBasicReady())
        {
            var inject = _injectWhenReady;
            _injectWhenReady = null;
            inject();
        }
        if (_typeAhead.Count == 0) return;
        int count = Memory.Ram[0xC6];
        int max = Math.Min((int)Memory.Ram[0x289], 10);
        if (max == 0) max = 10;
        while (count < max && _typeAhead.Count > 0)
        {
            Memory.Ram[0x277 + count] = _typeAhead.Dequeue();
            count++;
        }
        Memory.Ram[0xC6] = (byte)count;
    }

    /// <summary>State of the disk autostart helper.</summary>
    public enum AutostartState { Idle, WaitingForBasic, Loading, WaitingForRun, Done, Failed }

    /// <summary>Progress of <see cref="AutostartFromDisk"/>.</summary>
    public AutostartState Autostart { get; private set; } = AutostartState.Idle;
    private string _autostartFile = "*";
    private int _autostartFrames;

    /// <summary>
    /// Types LOAD"file",8,1 as soon as BASIC is ready, waits for the load to finish (READY. after LOADING)
    /// and then types RUN. Progress can be observed through <see cref="Autostart"/>; the helper is serviced at the
    /// end of every frame.
    /// </summary>
    public void AutostartFromDisk(string file = "*")
    {
        _autostartFile = file;
        _autostartFrames = 0;
        Autostart = AutostartState.WaitingForBasic;
    }

    public void CancelAutostart() => Autostart = AutostartState.Idle;

    private void ServiceAutostart()
    {
        switch (Autostart)
        {
            case AutostartState.WaitingForBasic:
                if (IsBasicReady())
                {
                    TypeText($"LOAD\"{_autostartFile}\",8,1\n");
                    Autostart = AutostartState.Loading;
                    _autostartFrames = 0;
                }
                break;
            case AutostartState.Loading:
            {
                _autostartFrames++;
                var screen = GetScreenText();
                int loading = screen.IndexOf("LOADING", StringComparison.Ordinal);
                if (screen.Contains("?FILE NOT FOUND", StringComparison.Ordinal) || screen.Contains("DEVICE NOT PRESENT", StringComparison.Ordinal))
                {
                    Autostart = AutostartState.Failed;
                }
                else if (loading >= 0 && screen.IndexOf("READY.", loading, StringComparison.Ordinal) >= 0 && Memory.Ram[0xC6] == 0)
                {
                    Autostart = AutostartState.WaitingForRun;
                    _autostartFrames = 0;
                }
                else if (_autostartFrames > 50 * 120) // two minutes: give up
                {
                    Autostart = AutostartState.Failed;
                }
                break;
            }
            case AutostartState.WaitingForRun:
                // A few frames of grace so the KERNAL has returned to the input loop.
                if (++_autostartFrames >= 5)
                {
                    TypeText("RUN\n");
                    Autostart = AutostartState.Done;
                }
                break;
        }
    }

    /// <summary>
    /// Loads a program into RAM the way LOAD"...",8 would (BASIC pointers updated) and optionally starts it
    /// with RUN (BASIC programs at $0801) or SYS (machine code). If BASIC is not ready yet, the injection is
    /// deferred until the READY prompt appears.
    /// </summary>
    public void InjectProgram(PrgFile program, bool run = true)
    {
        void DoInject()
        {
            int load = program.LoadAddress;
            int end = load + program.Data.Length;
            for (int i = 0; i < program.Data.Length; i++)
                Memory.Ram[(load + i) & 0xFFFF] = program.Data[i];
            // BASIC pointers: end of program / start of variables ($2D/$2E), arrays ($2F/$30), strings ($31/$32), LOAD end ($AE/$AF)
            Memory.Ram[0x2D] = Memory.Ram[0x2F] = Memory.Ram[0x31] = Memory.Ram[0xAE] = (byte)end;
            Memory.Ram[0x2E] = Memory.Ram[0x30] = Memory.Ram[0x32] = Memory.Ram[0xAF] = (byte)(end >> 8);
            if (run)
                TypeText(load == 0x0801 ? "RUN\n" : $"SYS{load}\n");
        }

        if (IsBasicReady())
            DoInject();
        else
            _injectWhenReady = DoInject;
    }

    #endregion
}
