# Architecture

## Input decision

The mod only arbitrates the shared physical right mouse button. Separate Bash
and Grapple keyboard bindings remain untouched.

1. `SeinSpiritLeashAbility.FindClosestAttackHandler` opens a target-search
   scope.
2. During that scope, the `Core.Input.Horizontal` and `Core.Input.Vertical`
   static fields temporarily contain the normalized direction from Ori's
   screen position to the mouse cursor. Native disassembly confirms that
   `FindClosestAttackHandler` reads these fields directly. The original values
   are restored immediately after the search. `SeinCharacter.FaceLeft` is
   aligned with the same direction.
3. Ori's original target search still performs range, line-of-sight, target
   type and ability checks.
4. The chosen `m_targetLeash` point is projected through the main camera.
5. A right-click inside the configured pixel radius is locked to Grapple;
   otherwise it is locked to Bash. The decision remains fixed until release.

## Failure behavior

The plugin resolves generated IL2CPP types at runtime. Required type or method
resolution is transactional: if any required patch cannot be installed, all
patches owned by this plugin are removed and original input behavior remains.

Individual routing failures also preserve the original input result and write
an error to `BepInEx/LogOutput.log`.

## Diagnostics

The first `GameController.OnGUI` callback creates a persistent Unity
`ScreenSpaceOverlay` Canvas with an `Image` panel and pooled `Text` components;
later callbacks only update their content, colors and pixel positions. This
avoids Ori's non-visible IMGUI and legacy GUIText paths. Grapple candidates are collected from
`CalculateAttackableCost`; the Bash candidate is captured from
`SeinBashAttack.FindClosestAttackHandler`. These observation patches do not
alter method arguments or results.

## Why runtime reflection

The game is IL2CPP and BepInEx creates the managed interop assemblies on its
first game launch. The project intentionally supports compilation before that
first launch by resolving those types at plugin load time. This also keeps the
repository small on a drive with limited free space.
