# Tedd.MOS65xx

A cycle-exact Commodore 64 emulator written in C# (.NET 11), with a real 1541 disk drive emulation and several
front-ends: a Windows desktop app (WPF), a browser build (Blazor WebAssembly), an SDL2 app and a Unity package.

Live demo and downloads: https://tedd.no/Tedd.MOS65xx/ (GitHub Pages, also reachable as https://tedd.github.io/Tedd.MOS65xx/; published from the `deploy` branch, see below). Binaries: https://github.com/tedd/Tedd.MOS65xx/releases/latest

## What is emulated

| Part | Model |
|---|---|
| CPU | Cycle-stepped NMOS 6502/6510: every clock is one bus cycle including dummy reads/writes, all 256 opcodes (documented and undocumented), NMOS decimal mode, RDY stalling, interrupt polling quirks (SEI/CLI/branch delays, NMI hijack), SO pin. Verified against all 2.56 million SingleStepTests/65x02 cycle-trace vectors and Klaus Dormann's functional test. |
| VIC-II | PAL 6569, per cycle: bad lines, all graphics modes, sprites with DMA timing and collisions, border unit, raster/collision/light-pen interrupts, register read-back rules. |
| CIA | Two 6526 with the Lorenz delay-pipeline timer model, TOD, serial register, old/new IRQ timing. |
| SID | 6581 following reSID: oscillators, ADSR with the rate-counter bugs, two-integrator filter, resampled output. |
| Memory | PLA with every LORAM/HIRAM/CHAREN/EXROM/GAME configuration incl. Ultimax, 6510 port, color RAM, open-bus reads. |
| Peripherals | Keyboard matrix (both scan directions), two joysticks, RESTORE, 8K/16K/Ultimax cartridges (raw and .CRT type 0), T64/PRG injection. |
| 1541 | Real drive: 6502 at 1 MHz, DOS ROM, two 6522 VIAs, IEC bus with the hardware ATN acknowledge, GCR bit stream at 300 RPM with the four density zones, writes go back into the D64. |

## Repository layout

```
src/Tedd.MOS65xx.Emulator   the emulator core (no UI, no dependencies)
src/Tedd.MOS65xx.Hosting    host-agnostic session, key bindings, video/audio sink contracts
src/Tedd.MOS65xx.GUI        Windows desktop front-end (WPF): memory viewer/editor, sprite viewer, character set viewer, key binding editor, audio visualizer
src/Tedd.MOS65xx.Web        Blazor WebAssembly front-end and the project web site
src/Tedd.MOS65xx.Sdl        SDL2 front-end (Windows/Linux/macOS)
src/Tedd.MOS65xx.Unity      game-engine facade (netstandard2.1) used by the Unity package in unity/
src/Tedd.MOS65xx.Tests      NUnit test-suite (~1400 tests)
docs/ARCHITECTURE.md        component contracts and timing model
```

## ROM images

The Commodore ROMs are copyrighted and are not part of the repository. Put them in a `roms` folder next to the
executable, in `src/Tedd.MOS65xx.GUI/` when working from source, or point the `C64_ROMS` environment variable
at them:

* `basic.901226-01.bin` (8 KiB) and `kernal.901227-03.bin` (8 KiB), or the combined `64c.251913-01.bin` (16 KiB)
* `characters.901225-01.bin` (4 KiB) or another `characters*.bin`
* optional 1541 ROM: `1541-II.251968-03.bin` (16 KiB) or the pair `1541-c000.325302-01.bin` + `1541-e000.901229-05.bin`

The web build can use [OpenROMs](https://github.com/MEGA65/open-roms) (a free LGPL re-implementation of
BASIC/KERNAL/character set, fetched by `tools/fetch-openroms.sh`; no 1541 DOS) and also lets you upload your
own ROMs, which stay in your browser's local storage.

## Building and testing

Requires the .NET 11 SDK.

```bash
dotnet build src/Tedd.MOS65xx.sln
dotnet test src/Tedd.MOS65xx.Tests/Tedd.MOS65xx.Tests.csproj
```

Slow and data-dependent suites:

* `Category=Slow` includes Klaus Dormann's functional test and the 1541 loading tests.
* `HARTE_6502_TESTS=<dir>` enables the 2.56 million SingleStepTests vectors (download `6502/v1/*.json` from
  https://github.com/SingleStepTests/65x02).
* `LORENZ_TESTS=<dir>` enables Wolfgang Lorenz's C64 test-suite (the `*.prg` files from VICE's
  `testprogs/general/Lorenz-2.15/src`), run with `dotnet test -c Release --filter Category=Lorenz`
  (`LORENZ_ONLY=cia1ta,irq` narrows it down). Status: all 221 CPU opcode programs, `trap1`-`trap16`,
  `branchwrap`, `mmufetch`, `mmu`, `cpuport`, `cputiming`, `irq` and every CIA timer program (`cia1ta/tb`,
  `cia2ta/tb`, `*pb6/7`, `*tb123`, `cia1tab`, `icr01`, `imr`, `flipos`, `oneshot`, `cntdef`, `cnto2`, `loadth`)
  pass; `nmi` (NMI arriving during BRK) and `trap17` are still open.

Rendering tests write PNG frames to `TestResults/` next to the test assembly.

## Front-ends

* **WPF** (`src/Tedd.MOS65xx.GUI`): attach D64/T64/PRG/CRT, autostart, screenshots, freeze + memory viewer/editor
  with cycle/instruction/frame stepping, sprite viewer (all eight sprites zoomed, with their registers, DMA state
  and position on screen), character set viewer (the ROM sets or the 2 KiB the VIC is reading live, per character
  screen/PETSCII codes and addresses, and loading a different character set over the running machine), key binding
  editor (Tools menu) where you pick the keys that make up the joysticks, audio visualizer window.
* **Web** (`src/Tedd.MOS65xx.Web`): runs entirely in the browser (video on a canvas, audio through an AudioWorklet),
  drag-and-drop media, touch joystick, built-in tech demo.
* **SDL2** (`src/Tedd.MOS65xx.Sdl`): `Tedd.MOS65xx.Sdl --roms <dir> --disk game.d64 --autostart`, game controller support.
* **Unity** (`unity/com.tedd.mos65xx`): UPM package with a `C64Emulator` MonoBehaviour that renders to a texture and
  plays audio through `OnAudioFilterRead`; build the DLLs with `tools/build-unity-package.ps1`.

All front-ends share `Tedd.MOS65xx.Hosting`: implement `IVideoSink`/`IAudioSink`, translate your key events to
W3C `KeyboardEvent.code` names and call `EmulatorSession.RunFrame()` from your loop (or use `EmulatorRunner`).

## Deployment

Pushing the `deploy` branch runs `.github/workflows/deploy.yml`: it tests the solution, publishes the Windows
build and the SDL2 builds as a GitHub Release, and publishes the web site + browser emulator to GitHub Pages
(enable *Settings > Pages > Source: GitHub Actions* once).

## License

MIT (see LICENSE). Test data under `src/Tedd.MOS65xx.Tests/TestData` has its own licenses (see the README there).
