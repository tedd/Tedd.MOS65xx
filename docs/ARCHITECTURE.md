# Tedd.MOS65xx architecture

Cycle-exact Commodore 64 emulator written in C# (.NET 8). Everything is clocked one system cycle at a
time; there are no "execute N cycles" shortcuts anywhere in the core. The same 6502 core drives the C64's
6510 and the 1541 drive's 6502.

```
src/Tedd.MOS65xx.Emulator   class library, no UI dependencies, no NuGet dependencies
src/Tedd.MOS65xx.Tests      NUnit tests
src/Tedd.MOS65xx.GUI        WPF front-end (Windows only)
```

ROM images, disk (.D64) and tape (.T64) images live in `src/Tedd.MOS65xx.GUI/` and are git-ignored.

## Conventions

* Namespaces: `Tedd.MOS65xx.Emulator.<Area>`; folders mirror namespaces.
* Every chip is a plain class with `Read(int reg)`, `Peek(int reg)` (no side effects, for debuggers),
  `Write(int reg, byte value)`, `Clock()` (exactly one cycle), `Reset()`.
* Register indexes passed to chips are already masked to the chip's register count; mirrors are handled by
  the memory map, not the chip.
* Signal lines are `bool` properties/fields where `true` means *asserted* (electrically low for the
  active-low lines IRQ, NMI, BA, ATN, CLK, DATA). Exception: port pin levels are raw voltages (`1` = high).
* No allocations in per-cycle paths. No LINQ in hot paths. `unsafe` is allowed but not required.
* Comments cite the source document for timing claims (64doc.txt, vic-ii.txt, CIA/VIA data sheets,
  "A Software Model of the CIA6526" by Wolfgang Lorenz, reSID).
* Public API is documented with XML comments. Tests use NUnit 4 (`Assert.That`).

## Bus / clock primitives — `Tedd.MOS65xx.Emulator.Bus`

```csharp
public interface IBus { byte Read(ushort address); void Write(ushort address, byte value); }
public interface IClockable { void Clock(); }
```

## CPU — `Tedd.MOS65xx.Emulator.Cpu.Cpu6502` (done, verified)

```csharp
public sealed class Cpu6502
{
    public Cpu6502(IBus bus);
    public byte A, X, Y, S; public ushort PC; public byte P;         // P: bit5 always 1, bit4 always 0 after PLP/RTI
    public bool FlagCarry, FlagZero, FlagInterrupt, FlagDecimal, FlagOverflow, FlagNegative;
    public bool Irq;              // level input, true = asserted
    public bool Nmi;              // edge input (false -> true triggers)
    public bool Rdy = true;       // false = halt on next read cycle (writes still run)
    public void SetOverflow();    // SO pin
    public void Clock();          // exactly one bus cycle
    public int  Step();           // run to next instruction boundary
    public void Reset();          // immediate: PC from $FFFC, S=$FD, I=1
    public bool AtInstructionBoundary { get; }
    public bool Jammed { get; }
    public long Cycles, StallCycles;
    public static OpcodeInfo GetOpcodeInfo(byte opcode);
}
```

Interrupt polling: lines are sampled at the end of every cycle; the poll at the end of an instruction uses
the sample from the previous cycle (two cycles back for a taken branch without page crossing). This gives
the documented SEI/CLI/PLP one-instruction delay and the branch delay.

## Machine timing model

One C64 system cycle (PAL: 985 248 Hz, 63 cycles per raster line, 312 lines per frame):

```
vic.Clock();                 // φ1 half: VIC memory access, BA/AEC, IRQ; renders 8 pixels
cpu.Rdy = !vic.Ba;
cpu.Irq = vic.Irq || cia1.IrqLine;
cpu.Nmi = cia2.IrqLine || restoreKeyPressed;
cpu.Clock();                 // φ2 half: CPU bus access (stalls when Rdy is false)
cia1.Clock(); cia2.Clock(); sid.Clock();
drive.ClockRatio();          // 1541 runs at 1 000 000 Hz; fractional accumulator keeps the ratio exact
```

## C64 memory map — `Tedd.MOS65xx.Emulator.C64.C64Memory : IBus, IVicMemory`

Implements the PLA. Inputs: 6510 port bits LORAM/HIRAM/CHAREN (with pull-ups when configured as inputs),
cartridge lines EXROM/GAME. Address decoding per the standard table (c64-wiki "Bank Switching"):

| LORAM HIRAM CHAREN | $8000 | $A000 | $D000 | $E000 |  (GAME=1 EXROM=1, no cartridge)
|---|---|---|---|---|
| 0 0 x | RAM | RAM | RAM | RAM |
| 0 1 0 | RAM | RAM | CHAR | KERNAL |
| 0 1 1 | RAM | RAM | I/O | KERNAL |
| 1 0 0 | RAM | RAM | RAM | RAM |
| 1 0 1 | RAM | RAM | I/O | RAM |
| 1 1 0 | RAM | BASIC | CHAR | KERNAL |
| 1 1 1 | RAM | BASIC | I/O | KERNAL |

8K cartridge (EXROM=0, GAME=1): ROML at $8000 when LORAM=HIRAM=1. 16K (EXROM=0, GAME=0): ROML at $8000
and ROMH at $A000 when HIRAM=1 (ROML additionally needs LORAM=1). Ultimax (EXROM=1, GAME=0): $1000-$7FFF
and $A000-$CFFF open (reads return the last value seen on the bus), ROML at $8000, I/O at $D000, ROMH at
$E000, regardless of the 6510 port.

Writes always go to RAM (write-through under ROM) except the I/O area when I/O is mapped, and open areas
in Ultimax mode.

I/O area ($D000-$DFFF when mapped):

| Range | Device |
|---|---|
| $D000-$D3FF | VIC-II, 64 registers mirrored every $40 |
| $D400-$D7FF | SID, 32 registers mirrored every $20 |
| $D800-$DBFF | Color RAM (1K x 4 bit; upper nibble reads as the last bus value) |
| $DC00-$DCFF | CIA 1, 16 registers mirrored |
| $DD00-$DDFF | CIA 2, 16 registers mirrored |
| $DE00-$DEFF | I/O 1 (cartridge, open) |
| $DF00-$DFFF | I/O 2 (cartridge, open) |

Unmapped I/O reads return the last value seen on the data bus (VICE "vicii_read_phi1" approximation:
the byte the VIC fetched in the φ1 half of the same cycle). `C64Memory` keeps `LastBusValue`.

VIC memory view (`IVicMemory`): 14-bit address + 2 bank bits from CIA2 port A (inverted: PA0-1 = 11 → bank 0
= $0000). Character ROM appears at $1000-$1FFF of banks 0 and 2. In Ultimax mode ROMH appears at
$3000-$3FFF of every bank.

```csharp
public interface IVicMemory
{
    byte ReadVic(int address14);   // 0..$3FFF inside the currently selected VIC bank
    byte ReadColor(int address10); // 0..$3FF, returns 0..15
    byte PeekVic(int address14);   // the same read without leaving the value on the bus (debuggers)
}
```

## VIC-II — `Tedd.MOS65xx.Emulator.Video.VicII` (PAL 6569)

Cycle-exact per Christian Bauer's "The MOS 6567/6569 video controller (VIC-II) and its application in
the Commodore 64" (vic-ii.txt). 63 cycles per line, 312 lines.

```csharp
public sealed class VicII : IClockable
{
    public VicII(IVicMemory memory);
    public byte Read(int reg); public byte Peek(int reg); public void Write(int reg, byte value); // reg 0..63
    public void Clock();                     // one system cycle (φ1 VIC access + 8 pixels)
    public void Reset();
    public bool Ba { get; }                  // true = BA asserted (CPU must stop on next read)
    public bool Aec { get; }                 // true = VIC owns the bus (3 cycles after Ba)
    public bool Irq { get; }                 // true = IRQ asserted
    public int RasterLine { get; }           // 0..311
    public int RasterCycle { get; }          // 1..63
    public long FrameCount { get; }
    public uint[] Frame { get; }             // ARGB, FrameWidth * FrameHeight, one full raster line = 504 px
    public const int FrameWidth = 504, FrameHeight = 312;
    public const int CyclesPerLine = 63, LinesPerFrame = 312;
    public static readonly uint[] Palette;   // 16 ARGB entries (Pepto PAL)
    public event Action? FrameCompleted;     // raised at the end of the last cycle of line 311
    public void SetLightPen(bool asserted);  // optional
}
```

Frame buffer pixel `x = (cycle - 1) * 8 + pixel`, `y = raster line`. The VIC renders the *entire* raster
line including blanking; the GUI crops to the visible PAL area. Sprites, all 5 graphics modes + 3 invalid
modes, border unit (38/40 columns, 24/25 rows), badlines (with DEN latch in line $30), idle state,
raster IRQ (line 0 fires one cycle later), sprite/sprite and sprite/background collisions, sprite DMA
timing (cycle 55/56 enable check, cycle 58 display check, MCBASE update in cycle 15/16), sprite
priorities, X/Y expansion, multicolor sprites, XSCROLL/YSCROLL, register read-back rules ($D011/$D012
return the current raster, unused bits read 1, $D01E/$D01F cleared on read, $D019 acknowledge by writing 1).

Per-line VIC φ1 access schedule (6569):

| Cycle | Access |
|---|---|
| 1 | sprite 3 pointer (p), then data (s) at 1φ2, 2φ1, 2φ2 |
| 3,5,7,9 | sprite 4,5,6,7 pointer + data |
| 11-15 | DRAM refresh |
| 15-54 (φ2) | c-accesses (video matrix + color RAM) on bad lines |
| 16-55 (φ1) | g-accesses (character generator / bitmap) |
| 55-56 | sprite DMA enable check (Y compare); MC/MCBASE handling |
| 58 | sprite 0 pointer + data; display state check (RC=7 → idle) |
| 60,62 | sprite 1, 2 pointer + data |

BA goes low three cycles before the first access the VIC needs the bus for (cycle 12 on a badline,
three cycles before a sprite's pointer fetch when that sprite's DMA is on).

## CIA 6526 — `Tedd.MOS65xx.Emulator.IO.Cia6526`

```csharp
public sealed class Cia6526 : IClockable
{
    public Cia6526(string name);
    public byte Read(int reg); public byte Peek(int reg); public void Write(int reg, byte value); // reg 0..15
    public void Clock(); public void TodTick(); public void Reset();
    public Func<byte>? PortAInput;   // external pin levels, 1 = high; default 0xFF
    public Func<byte>? PortBInput;
    public byte PortAOutput { get; } // PRA | ~DDRA   (what the chip drives; inputs float high)
    public byte PortBOutput { get; } // PRB | ~DDRB, with timer PB6/PB7 output overlaid when enabled
    public event Action? PortAChanged; // after any write that can change PortAOutput
    public event Action? PortBChanged;
    public bool Flag { set; }        // FLAG pin; a true->false transition sets ICR bit 4
    public bool IrqLine { get; }     // true = /IRQ asserted
    public bool Model6526A { get; set; } // false = old 6526 (IRQ one cycle after ICR bit), true = 6526A
}
```

Reading PRA/PRB returns `PortXOutput & PortXInput()`. Timer model follows Lorenz's software model:
the first decrement happens two cycles after the start bit is written; underflow sets the ICR bit in the
same cycle, reloads from the latch (new value visible the next cycle), stops the timer in one-shot mode,
and the IRQ line follows one cycle later on the old 6526. Timer B can count timer A underflows (CRB bits
5-6). TOD: BCD tenths/seconds/minutes/hours with AM/PM, halt on hours write until tenths write, latch on
hours read, alarm (CRB bit 7 selects alarm write), 50/60 Hz divider selected by CRA bit 7. Serial shift
register: output mode shifts on timer A underflow (8 bits then ICR bit 3); input mode minimal.
ICR: reading returns pending bits with bit 7 set if any enabled bit is pending, then clears them; writing
with bit 7 set sets mask bits, with bit 7 clear clears them.

## VIA 6522 — `Tedd.MOS65xx.Emulator.IO.Via6522`

```csharp
public sealed class Via6522 : IClockable
{
    public Via6522(string name);
    public byte Read(int reg); public byte Peek(int reg); public void Write(int reg, byte value); // reg 0..15
    public void Clock(); public void Reset();
    public Func<byte>? PortAInput; public Func<byte>? PortBInput;   // pin levels, default 0xFF
    public byte PortAOutput { get; } public byte PortBOutput { get; } // ORx | ~DDRx (T1 PB7 overlaid when enabled)
    public event Action? PortAChanged; public event Action? PortBChanged;
    public bool Ca1 { get; set; } public bool Ca2 { get; set; } public bool Cb1 { get; set; } public bool Cb2 { get; set; } // input levels (raw)
    public bool Ca2Output { get; } public bool Cb2Output { get; }   // when PCR configures them as outputs
    public event Action? Ca2Changed; public event Action? Cb2Changed;
    public bool IrqLine { get; }
}
```

Registers: 0 ORB/IRB, 1 ORA/IRA (with handshake), 2 DDRB, 3 DDRA, 4 T1C-L, 5 T1C-H, 6 T1L-L, 7 T1L-H,
8 T2C-L, 9 T2C-H, 10 SR, 11 ACR, 12 PCR, 13 IFR, 14 IER, 15 ORA (no handshake). Timer 1: one-shot /
free-running with reload from latch, optional PB7 output; interval is N+2 cycles in free-running mode.
Timer 2: one-shot or PB6 pulse counting. CA1/CB1 edge interrupts with polarity from PCR, CA2/CB2 input
edge / independent modes, handshake and pulse output modes, manual output. Reading/writing ORA/ORB
clears the corresponding CA/CB interrupt flags as documented. IFR bit 7 = any enabled flag set.

## SID 6581 — `Tedd.MOS65xx.Emulator.Audio.Sid6581`

```csharp
public sealed class Sid6581 : IClockable
{
    public Sid6581();
    public byte Read(int reg); public byte Peek(int reg); public void Write(int reg, byte value); // reg 0..31
    public void Clock(); public void Reset();
    public float Output { get; }         // mixed, filtered, volume-scaled output after the last Clock(), range about -1..1
    public byte PotX { get; set; } public byte PotY { get; set; } // paddle inputs (default 0xFF)
}
public sealed class SidResampler
{
    public SidResampler(Sid6581 sid, int sampleRate, double clockFrequency = 985248.0);
    public void Clock();                                  // call once per system cycle; runs sid.Clock()
    public int Read(Span<short> destination);             // drain resampled 16-bit mono PCM
    public int Available { get; }
}
```

Voices per reSID: 24-bit phase accumulator, triangle/saw/pulse/noise (23-bit LFSR clocked on accumulator
bit 19 rising edge, output bits 20,18,14,11,9,5,2,0), combined waveforms approximated with AND,
oscillator sync and ring modulation, test bit. Envelope: ADSR rate table (period values 9, 32, 63, 95, 149,
220, 267, 313, 392, 977, 1954, 3126, 3907, 11720, 19532, 31251), exponential counter for decay/release
(1/2/4/8/16/30 steps at levels $FF/$5D/$36/$1A/$0E/$06), gate handling. Filter: two-integrator-loop
state-variable filter, LP/BP/HP mixing, per-voice routing, "3 OFF", cutoff from the 11-bit value using the
6581 curve, resonance to Q mapping; master volume. Reads: $19/$1A POTX/POTY, $1B OSC3 (upper 8 bits of
voice 3 waveform), $1C ENV3; write-only registers read as the last written bus value (return 0).

## IEC serial bus — `Tedd.MOS65xx.Emulator.IO.IecBus`

Open-collector lines ATN, CLK, DATA: a line is *low* (asserted) if any participant pulls it low.

```csharp
public sealed class IecBus
{
    public bool AtnLow { get; } public bool ClkLow { get; } public bool DataLow { get; }
    public IecPort Attach(string name);          // one port per participant
    public event Action? Changed;                // any line changed
}
public sealed class IecPort
{
    public bool PullAtn { get; set; } public bool PullClk { get; set; } public bool PullData { get; set; }
    public IecBus Bus { get; }
}
```

C64 side (CIA2 PA): PA3=1 pulls ATN, PA4=1 pulls CLK, PA5=1 pulls DATA; PA6 reads 1 when CLK is *high*
(released), PA7 reads 1 when DATA is high. 1541 side (VIA1 PB): PB0 reads 1 when DATA is *low*, PB2 reads
1 when CLK is low, PB7 reads 1 when ATN is low; PB1=1 pulls DATA, PB3=1 pulls CLK, PB4 = ATNA. Hardware
auto-acknowledge: DATA is also pulled low whenever `AtnLow != ATNA`. ATN in also drives VIA1 CA1.

## 1541 drive — `Tedd.MOS65xx.Emulator.Drive`

* `Drive1541`: `Cpu6502` + `DriveMemory` (2K RAM at $0000-$07FF mirrored through $1FFF; VIA1 at $1800,
  VIA2 at $1C00 (each mirrored in its $400 window); 16K ROM at $C000-$FFFF) + two `Via6522` + `DiskUnit`.
  `Clock()` = one 1 MHz drive cycle. `Attach(GcrDisk)`, `Detach()`, `DeviceNumber` (8-11, VIA1 PB5/PB6).
* `DiskUnit` (mechanics): 300 RPM spindle, 84 half-tracks, stepper (VIA2 PB0-1 phase pattern), motor
  (PB2), LED (PB3), write protect (PB4, 1 = write enabled), density (PB5-6 select the bit rate: 4 zones
  with 16/15/14/13 cycles per bit i.e. 32/30/28/26 cycles per byte... see below), SYNC (PB7 = 0 while
  10+ consecutive 1 bits are under the head), byte-ready → VIA2 CA1 negative edge and CPU SO (when SOE via
  CA2 is enabled), read data → VIA2 PA input, write via PA output when CB2 (R/W mode) = 0.
  Bit rates: zone 0 (tracks 31-35) 13 cycles/bit, zone 1 (25-30) 14, zone 2 (18-24) 15, zone 3 (1-17) 16
  — selected by PB5-6 (the ROM sets them from the track number, the mechanics only use PB5-6).
* `GcrDisk`: 84 half-tracks of raw bit streams. `FromD64(D64Image)` produces standard formatted tracks
  (sync, header, gap, sync, data, gap; GCR 4→5 encoding; track lengths 7692/7142/6666/6250 bytes for the
  four zones); `ToD64()` decodes back. Odd half-tracks are empty (unformatted).
* `D64Image`: sector access, directory listing, file read (chain following), BAM, disk name/ID; 35-track
  (174848 bytes, optionally + 683 error bytes) and 40-track variants.

## Tape / program files — `Tedd.MOS65xx.Emulator.Media`

* `T64Image`: parses the "C64 tape image file"/"C64S tape file" container; entries with name, C64 file
  type, start/end address, data. Works around the common wrong-end-address bug (use file size).
* `PrgFile`: 2-byte little-endian load address + data.
* Loading into the C64 is done by injection: after the KERNAL is ready, data is copied to RAM at the load
  address and BASIC pointers ($2B-$2E, $AE/$AF) are set, then `RUN` is typed via the keyboard buffer.

## Assembler — `Tedd.MOS65xx.Emulator.Tools.Assembler`

Two-pass 6502 assembler used by the tests to inject compiled programs.

```csharp
public sealed class AssemblyResult { public ushort Origin; public byte[] Bytes; public IReadOnlyDictionary<string, ushort> Labels; }
public static class Assembler { public static AssemblyResult Assemble(string source, ushort defaultOrigin = 0x1000); }
public sealed class AssemblerException : Exception { public int Line { get; } }
```

Syntax: `* = $C000` or `.org $C000`; labels `name:` (also `name` at line start followed by an instruction);
`sym = expr`; comments `;`; `.byte`/`.db`, `.word`/`.dw`, `.text "..."`, `.res n[, fill]`; operands with
`#imm`, `<expr` / `>expr` low/high byte, `*` current address, expressions with `+ - * / & | ^ << >> ( )`,
numbers `$hex %bin decimal 'c'`; all addressing modes incl. `(zp,X)`, `(zp),Y`, `(abs)`; zero page is
chosen automatically when the operand is known and < 256 and the mode exists; forward references assemble
as absolute; undocumented mnemonics (SLO RLA SRE RRA SAX LAX DCP ISC ANC ALR ARR ANE/XAA SBX/AXS LAS
SHA/AHX SHX SHY TAS/SHS JAM/KIL NOP-with-operand) supported.

## Sprite snapshot — `Tedd.MOS65xx.Emulator.Tools.SpriteSnapshot`

Decodes the eight VIC-II sprites for debuggers and viewers without disturbing the machine: registers through
`VicII.Peek` (so `$D01E`/`$D01F` are *not* cleared) and memory through `IVicMemory.PeekVic` (so nothing is left
on the bus). `Update` refills the same instance, so a viewer that refreshes several times per second allocates
nothing.

```csharp
public sealed class SpriteSnapshot
{
    public const int Width = 24, Height = 21, DataSize = 63;
    public const byte Transparent = 0xFF;
    public static SpriteSnapshot Capture(VicII vic, int vicBank = 0);
    public void Update(VicII vic, int vicBank = 0);
    public IReadOnlyList<SpriteInfo> Sprites { get; }   // also this[n]
    // $D015, $D017, $D01B-$D01F, $D021, $D025/$D026, video matrix, bank base, active DMA count
}

public sealed class SpriteInfo
{
    // MxX/MxY (also relative to the display window), color, multicolor/expansion/priority flags, collisions,
    // pointer and data address (inside the bank and as the CPU sees it), the 63 data bytes, and the sequencer
    // state: DMA on, display state, MC/MCBASE, Y expansion flip-flop, shift register.
    public void Render(Span<byte> pixels);   // 24 x 21 color indices, Transparent where nothing is drawn
    public int SolidPixelCount();
}
```

The sequencer state comes from `VicII.SpriteX/SpriteDma/SpriteDisplayed/SpriteExpansionFlipFlop/SpriteMc/`
`SpriteMcBase/SpritePointer/SpriteShiftRegister(int n)`, all side effect free. The WPF sprite viewer
(`SpriteViewerWindow`, `SpriteViews.cs`) is a thin layer on top: thumbnails, a zoomed pixel grid, where each
sprite sits relative to the display window, and the register/DMA text.

## PNG output — `Tedd.MOS65xx.Emulator.Tools.PngWriter`

`static byte[] Encode(int width, int height, ReadOnlySpan<uint> argb)` and
`static void Save(string path, int width, int height, ReadOnlySpan<uint> argb)`; zlib via
`System.IO.Compression.ZLibStream`, no other dependencies. Used by tests and the GUI screenshot feature.

## Hosting layer — `src/Tedd.MOS65xx.Hosting`

Everything a front-end needs, independent of UI framework. Front-ends (WPF, Blazor/WASM, SDL2, Unity) only
implement the two sink interfaces and translate their native key events to W3C `KeyboardEvent.code` names.

```csharp
public interface IVideoSink { void PresentFrame(in VideoFrame frame); }   // frame.CopyVisible/Rgba/Bgra helpers, 384x272
public interface IAudioSink { int SampleRate { get; } void Write(ReadOnlySpan<short> samples); void Clear(); }
public sealed class AudioTap : IAudioSink          // decorator keeping the latest samples for visualizers (CopyLatest)

public sealed class EmulatorSession                 // single-threaded; call RunFrame() from the host's loop
{
    EmulatorSession(RomSet roms, int sampleRate = 44100, bool attachDrive = true, KeyBindings? bindings = null);
    C64 Machine; KeyBindings Bindings; IVideoSink Video; IAudioSink Audio; bool Paused; bool Warp;
    void RunFrame(); void StepCycle(); void StepInstruction(); void Reset(bool hard);
    void KeyDown(string code); void KeyUp(string code); void SetJoystick(int port, JoystickInput input, bool pressed);
    void SetKey(C64Key key, bool pressed); void ReleaseAllInput(); void TypeText(string text);
    event Action<SystemCommand> Command;            // bound host commands (Reset, HardReset, Pause, Warp, Screenshot, MemoryViewer)
    void AttachDisk(byte[] d64, string name, bool autostart, bool writeProtected = false); void EjectDisk(); D64Image? SaveDisk();
    void AttachProgram(byte[] t64OrPrg, string name, int entryIndex = 0, bool run = true);
    void AttachCartridge(byte[] rawOrCrt, string name); void DetachCartridge();
    void AttachAuto(byte[] data, string fileName, bool autostart = true);   // by extension / content
    string MediaDescription;
}
public sealed class EmulatorRunner : IDisposable    // paced thread for hosts without their own loop
{
    EmulatorRunner(EmulatorSession session); void Start(); bool Paused; bool Warp; double MeasuredFps;
    void Invoke(Action a); T Invoke<T>(Func<T> f);  // exclusive access to the machine (between frames / while frozen)
}
public sealed class KeyBindings                     // code -> InputAction, JSON { "KeyA": "key:A", "Numpad8": "joy2:up", "PageUp": "sys:restore" }
{
    static KeyBindings CreateDefault(); static KeyBindings FromJson(string); string ToJson(); Save/Load/LoadOrDefault(path);
    InputAction? Get(string code); bool TryGet(...); void Set(string code, InputAction a); bool Remove(string code); IEnumerable<string> CodesFor(InputAction a);
    event Action Changed; IReadOnlyDictionary<string, InputAction> All;
}
public readonly struct InputAction                  // ForKey(C64Key, shift) | ForJoystick(port 1|2, JoystickInput) | ForSystem(SystemCommand); Parse/ToString/Describe
public static class KeyCodes                        // All (W3C code names), Display(code)
```

Key naming: W3C `KeyboardEvent.code` ("KeyA", "Digit1", "Enter", "ShiftLeft", "ArrowUp", "Numpad8", "F1"...).
Browsers deliver these directly; WPF, SDL2 and Unity hosts keep a table from their native key enums.

## Tests — `src/Tedd.MOS65xx.Tests`

* `Support/RecordingBus` — 64K RAM that logs every bus access (`BusAccess(Address, Value, IsWrite)`).
* `Cpu/HarteSingleStepTests` — SingleStepTests vectors (needs `HARTE_6502_TESTS`).
* `Cpu/Opcode*Tests` — hand-written per-opcode tests: exact cycle traces per addressing mode (from
  64doc.txt), flag behaviour, page crossing, RMW double write, stack, interrupts.
* `Cpu/AssemblyProgramTests` — programs written in assembly, assembled and run.
* `Cpu/FunctionalTests` — Klaus Dormann's 6502 functional test (`TestData/6502_functional_test.bin`).
* Chip tests next to each chip; VIC tests write PNGs to `TestResults/` for inspection and compare against
  reference images in `TestData/Reference/`.
* System tests boot the real ROMs (located via `RomSet.Locate()`), so they are skipped when ROMs are
  missing.
