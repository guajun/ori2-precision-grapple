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
  routing work; the current `0.6.0` build adds live condition diagnostics for
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

- `RadiusPixelsAt1080p = 80`
- `ReferenceHeight = 1080`
- `TargetMarkerRadiusPixelsAt1080p = 11`
- `HideImpreciseGrappleMark = true`
- `DebugLogging = false`
- `ExternalMonitorEnabled = true`
- `ShowOverlay = false`
- `ShowWorldMarkers = true`

The external Windows monitor draws a large mouse-acceptance circle, small rings
at the game's actual hook points, normal and retained-target range boundaries,
and a short reason beside every observed candidate. Colors distinguish ready,
cursor miss, vanilla selector conflict, direction rejection, retained-only
range, out of range, and global cooldown/state blockers. The HUD also shows the
underlying costs, angles, cooldowns, ranges, Bash target and final input route.
Candidates are observed after Ori's early type/visibility filters, so an object
which never reaches target scoring will not receive a candidate marker.

The monitor uses a local named pipe and does not depend on Ori's custom
rendering pipeline.

```powershell
.\scripts\start-monitor.ps1
```

For normal use, double-click `Start Monitor.cmd` in the project root. It starts
the already-built monitor and rebuilds it automatically if the executable is
missing.

The monitor may start before or after Ori and reconnects automatically. The
experimental in-game overlay is disabled by default.

The transparent click-through overlay follows the Ori client area and supports
windowed and borderless fullscreen modes. A true exclusive-fullscreen swap
chain can cover all external top-level windows; for reliable overlay rendering,
set these Steam launch options for this Unity 2018 game:

```text
-popupwindow -screen-fullscreen 0
```

Optionally add `-screen-width 1920 -screen-height 1080` for a fixed resolution.

## Safety

Back up `%LOCALAPPDATA%\Ori and the Will of The Wisps` before gameplay testing.
The plugin does not intentionally write save data. See
`docs/FIRST_RUN_CHECKLIST.md` before the first launch.

## License

MIT. See `THIRD_PARTY_NOTICES.md` for BepInEx and community reference details.
