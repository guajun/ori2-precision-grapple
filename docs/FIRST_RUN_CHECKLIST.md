# First-run checklist

Use this checklist for every fresh machine or BepInEx installation.

1. Back up saves from `%LOCALAPPDATA%\Ori and the Will of The Wisps`.
2. Run `scripts/install.ps1` while Ori is closed.
3. Launch Ori manually and wait for BepInEx's first-time IL2CPP generation.
4. Close Ori and inspect `BepInEx/LogOutput.log` for:
   - `Ori Precision Grapple 0.2.0 loaded`
   - no `Missing interop types` or patch rollback message
5. Test right-click away from targets: Bash should begin.
6. Test right-click directly on blue moss, hooks and eligible enemies: Grapple
   should begin.
7. Hold right-click and move the cursor across a target boundary: the chosen
   action must not change until release.
8. Test separate `F` and `G` bindings, menus, map view, pause, death/reload and
   cutscenes.
9. Repeat at 1080p and the normal fullscreen resolution.

Enable `Diagnostics.DebugLogging` in
`BepInEx/config/io.github.guajun.ori2precisiongrapple.cfg` for route traces.
