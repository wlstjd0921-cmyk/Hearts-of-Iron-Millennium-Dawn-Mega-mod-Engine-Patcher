# Hoi4 Engine Patcher Made with ClaudeCode 

Source code of the engine patcher that ships with **Megamod** ([Steam Workshop item 3820172588](https://steamcommunity.com/sharedfiles/filedetails/?id=3820172588)), a map mod for Hearts of Iron IV with Millennium Dawn.

Megamod's map is 7424 × 3072 pixels (22.8 million). The unmodified HOI4 engine cannot load a map that large and crashes while loading it. The patcher edits `hoi4.exe` to raise those limits. The mod does not run without it.

## Released versions

| Version | For HOI4 | Published on the Workshop | SHA-256 of `Hoi4 Engine Patcher.exe` |
|---|---|---|---|
| 2.1.0 | 1.19.4.0 | 2026-10-28, about 14:10 UTC | `77F652DB03DD51052A0F63F2B702775B97CC54FA076BE6ECCF862D9E521913CA` |
| 2.1.1 | 1.19.4.1 | 2026-10-29, 17:42 UTC | `DCCAB54BD1C9DB72A14BE05B67F0AC0FA0ADC7A2FF612B13F1C8CEF69B0D58A8` |

HOI4 1.19.4.1 (released 2026-10-29 17:02 UTC) recompiled `hoi4.exe`, which moved the code the patcher edits. Version 2.1.1 contains the new locations. Each version only works on its own HOI4 version and refuses to write anything to any other.

## What it changes in `hoi4.exe`

- Raises the engine's map size limit so a 7424 × 3072 map can load (the stock engine stops at about 13.2 million pixels).
- Raises three internal limits: a scratch buffer from 4 to 64 MB, the map pixel buffer from 50 to 96 MB, and the texture file size cap from 16 to 64 MB.
- Adds a guard against a divide-by-zero crash in strategic-region code.

In total: 37 short edits in the `.text` section, plus one new section `.fwmall` that holds 7 small routines for edits that do not fit in place. The exception table is extended so those routines unwind correctly. The patch data is in `src/PatchData.g.cs`, and the code that applies it is `Engine.Apply` in `src/Engine.cs`.

## How it behaves

- Before writing anything, it compares the original bytes at all 37 locations. If any of them differ (a different game version), it writes nothing.
- It does nothing if `hoi4.exe` is already patched, and it refuses to patch while the game is running.
- It writes the patched `hoi4.exe` in place. **It does not keep a backup copy.** To undo the patch, use Steam → Hearts of Iron IV → Properties → Installed Files → *Verify integrity of game files*.
- It contains no network code.

Other files it touches:

- `%LOCALAPPDATA%\Hoi4EnginePatcher\lang.txt` (the chosen language) and `chain.log` (a launch log, deleted once it exceeds 512 KB).
- **Only if you tick the "run automatically" box:** it sets HOI4's Steam launch option to `"<patcher path>" %command%` in Steam's `userdata\<account>\config\localconfig.vdf`. To do that it asks first, closes Steam (`steam.exe -shutdown`), edits the file and starts Steam again. This replaces any existing HOI4 launch options, and unticking the box clears them (`src/Steam.cs`).
- When Steam starts the patcher through that launch option, it checks whether `hoi4.exe` is patched and shows the patch window if not. Then it starts the original game command (the Paradox Launcher) and waits until the launcher and game processes have exited (`RunChain` in `src/Program.cs`).

## Why antivirus software may flag it

The patcher rewrites another program's executable, which is also what some malware does. The file is also unsigned and new, so it has no download reputation. Heuristic and machine-learning detections, such as Malwarebytes' `MachineLearning/Anomalous`, can react to this. You do not have to rely on that explanation: you can build the released files yourself from this source and compare the hashes.

## Build and verify

Requirements (Windows):

- .NET SDK **9.0.302**. The build uses its Roslyn compiler at `C:\Program Files\dotnet\sdk\9.0.302\Roslyn\bincore\csc.dll`.
- .NET Framework 4.8 Developer Pack (the reference assemblies in `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8`).

Then run:

```
build.cmd v2.1.0
build.cmd v2.1.1
```

Each command builds `vX.Y.Z\out\Hoi4 Engine Patcher.exe` and prints its SHA-256. Compare it with the table above or `SHA256SUMS.txt`. The build is deterministic (`-deterministic`), so the same compiler version produces a byte-identical file. Line endings in the source do not affect the result: building with all-LF and all-CRLF source gives the same hash. A different compiler version may produce a different binary.

`tools/csc.sh` is the Git Bash script used for the releases. It runs the same compiler command:

```
cd v2.1.1
mkdir -p out
../tools/csc.sh "out/Hoi4 Engine Patcher.exe" winexe -win32icon:app.ico -win32manifest:app.manifest "-resource:app.ico,Hoi4EnginePatcher.app.ico" -- src/AssemblyInfo.cs src/Engine.cs src/Forms.cs src/PatchData.g.cs src/Program.cs src/Steam.cs src/Texts.g.cs src/Theme.cs
```

## Repository layout

- `v2.1.0/`, `v2.1.1/`: the source exactly as each release was built (`src/`, `app.ico`, `app.manifest`). Only `AssemblyInfo.cs`, `PatchData.g.cs` and `Texts.g.cs` differ between the two.
- The `*.g.cs` files were generated from the patcher's data script and are committed as built.
- `src/PatchData.g.cs` contains short byte sequences copied from `hoi4.exe`: the original bytes at each location, used for the check before writing. The released executables contain the same bytes.
- Some code comments and log messages are in Korean. `Texts.g.cs` still contains a few strings (backup and restore) left over from an earlier PowerShell version. The C# patcher does not use them.

## Disclaimer

This project is not affiliated with Paradox Interactive or Valve. Hearts of Iron IV is a trademark of Paradox Interactive.
