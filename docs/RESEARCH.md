# Reverse-engineering notes

Local game fingerprint captured on 2026-09-01:

- Unity: `2018.4.24.7322101`
- Executable SHA-256: `7F6BF137F31C6E0FB644103A87A0CC66B9354CFD29C97D0EA2751407FF601D22`
- GameAssembly SHA-256: `475827A37396BDFBC6678B8C430F02BCFB6FCAE47162D570266911647A341A89`
- global-metadata SHA-256: `C3704EEAAB71F0199CFDA92DD209799A168BF0F9229E8EB8BECD0446D52B35BE`

Relevant methods confirmed from the Ori community randomizer client at commit
`e62e92e8dc6c40915a17a79464477c93cce30965`:

- `SeinSpiritLeashAbility.FindClosestAttackHandler`
- `SeinSpiritLeashAbility.IsInputTowardsTarget`
- `SeinSpiritLeashAbility.ShouldShowMark`
- `SeinCharacter.get_FaceLeft`
- `SmartInput.CompoundButtonInput.GetValue`
- `SmartInput.CachedButtonInput.GetButton`

The community implementation already demonstrates mouse-direction injection
for Grapple. This project adds precise screen-space hit testing and shared
right-click arbitration.

The BepInEx 6.0.0-pre.2 Harmony backend cannot safely patch this build's
`IsInputTowardsTarget` method because its `ref float` argument causes native to
managed trampoline failures. Native disassembly also shows that
`FindClosestAttackHandler` does not call the `Core.Input.Axis` getter. It reads
the `Core.Input.Horizontal` and `Core.Input.Vertical` static fields directly,
so the BepInEx implementation temporarily overrides those two fields.

BepInEx generates the game's primary interop assembly as `__mainWisp.dll`, not
the conventional `Assembly-CSharp.dll`.
