# Ori Precision Grapple

An experimental BepInEx IL2CPP mod for *Ori and the Will of the Wisps*.

Default shared right-click behavior becomes:

- cursor precisely on a valid Grapple target: Grapple;
- otherwise: Bash.

The choice is locked from press to release. Separate keyboard bindings are not
changed.

## Current status

- Pure routing and precision math: implemented and tested (10 cases).
- BepInEx plugin: compiles against BepInEx 6.0.0-pre.2.
- Runtime IL2CPP patch layer: implemented using generated-type discovery and
  exercised against a fake Ori API with real Harmony patches.
- In-game validation: active. Mouse-directed Grapple selection and right-click
  routing work; the current `0.2.0` build adds live condition diagnostics for
  tuning and edge-case investigation.

The project remains experimental and has not been packaged as a stable release.

## Build

Requirements: Windows, PowerShell 7 and .NET SDK 8.

```powershell
cd ori2-precision-grapple
.\scripts\build.ps1
```

The script verifies/downloads the pinned BepInEx SDK, builds both assemblies,
runs the dependency-free core test suite, and creates `artifacts/OriPrecisionGrapple`.

## Install without launching Ori

```powershell
.\scripts\install.ps1
```

The default game directory is:

`D:\SteamLibrary\steamapps\common\Ori and the Will of the Wisps`

The installer refuses to run while Ori is open and refuses to overwrite an
unknown root loader. It does not launch Ori or Steam.

## Configuration

BepInEx creates `BepInEx/config/io.github.guajun.ori2precisiongrapple.cfg` after the first
plugin load. Important settings:

- `RadiusPixelsAt1080p = 48`
- `ReferenceHeight = 1080`
- `HideImpreciseGrappleMark = true`
- `DebugLogging = false`
- `ShowOverlay = true`
- `ShowWorldMarkers = true`

The diagnostics overlay shows the vanilla Grapple target and candidates,
precision radius, mouse-to-target distance, cooldowns, ranges, Bash target and
the final input route. It is enabled for the current investigation build.

## Safety

Back up `%LOCALAPPDATA%\Ori and the Will of The Wisps` before gameplay testing.
The plugin does not intentionally write save data. See
`docs/FIRST_RUN_CHECKLIST.md` before the first launch.

## License

MIT. See `THIRD_PARTY_NOTICES.md` for BepInEx and community reference details.
