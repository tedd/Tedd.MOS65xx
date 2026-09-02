# Plugins

This folder receives the three .NET Standard 2.1 assemblies the package runs on:

* `Tedd.MOS65xx.Emulator.dll` - the machine (CPU, VIC-II, SID, CIAs, 1541, media formats)
* `Tedd.MOS65xx.Hosting.dll` - session, key bindings, video/audio sink contracts
* `Tedd.MOS65xx.Unity.dll` - the `C64Bridge` facade and the `UnityKeyCodes` table

The DLLs are build output and are **not committed**. Produce them from the repository root with

```
pwsh tools/build-unity-package.ps1     # Windows / PowerShell 7
sh   tools/build-unity-package.sh      # macOS / Linux
```

which runs `dotnet build -c Release -f netstandard2.1` for the three projects and copies the results (and their
`.pdb` files) here. `Runtime/Tedd.MOS65xx.asmdef` references the DLLs by name (`precompiledReferences`), so Unity
picks them up as soon as they exist; the `C64Emulator` component does not compile until they do.
