using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace OriPrecisionGrapple.Runtime;

internal sealed class GamePatchInstaller
{
    private readonly Harmony _harmony;
    private readonly GameRuntime _runtime;
    private readonly ManualLogSource _log;

    public GamePatchInstaller(Harmony harmony, GameRuntime runtime, ManualLogSource log)
    {
        _harmony = harmony;
        _runtime = runtime;
        _log = log;
    }

    public bool TryInstall()
    {
        if (!GameTypeCatalog.TryCreate(out var types, out var error))
        {
            _log.LogError(error);
            return false;
        }

        try
        {
            _runtime.Attach(types!);
            PatchCallbacks.Attach(_runtime);

            PatchRequired(
                GameTypeCatalog.FindMethod(types!.CompoundButtonInput, "GetValue", 0),
                postfix: nameof(PatchCallbacks.ButtonResultPostfix));

            if (types.CachedButtonInput is not null)
            {
                PatchOptional(
                    GameTypeCatalog.FindMethod(types.CachedButtonInput, "GetButton", 0),
                    postfix: nameof(PatchCallbacks.ButtonResultPostfix));
            }

            PatchRequired(
                GameTypeCatalog.FindMethod(types.SpiritLeash, "FindClosestAttackHandler", 0),
                prefix: nameof(PatchCallbacks.TargetSearchPrefix),
                postfix: nameof(PatchCallbacks.TargetSearchPostfix));
            PatchOptional(
                GameTypeCatalog.FindMethod(types.SpiritLeash, "CalculateAttackableCost", 4),
                postfix: nameof(PatchCallbacks.GrappleCostPostfix));
            PatchRequired(
                GameTypeCatalog.FindMethod(types.SeinCharacter, "get_FaceLeft", 0),
                prefix: nameof(PatchCallbacks.FaceLeftPrefix));

            if (types.BashAttack is not null)
            {
                PatchOptional(
                    GameTypeCatalog.FindMethod(types.BashAttack, "FindClosestAttackHandler", 0),
                    postfix: nameof(PatchCallbacks.BashTargetPostfix));
            }

            if (types.GameController is not null && types.Gui is not null && types.Rect is not null && types.Color is not null)
            {
                var onGui = GameTypeCatalog.FindMethod(types.GameController, "OnGUI", 0);
                if (onGui is null)
                {
                    _log.LogWarning("Diagnostics overlay hook unavailable: GameController.OnGUI was not found.");
                }
                else
                {
                    Patch(onGui, null, nameof(PatchCallbacks.DebugOverlayPostfix));
                    _log.LogInfo("Diagnostics overlay hook installed on GameController.OnGUI.");
                }
            }
            else
            {
                _log.LogWarning("Diagnostics overlay hook unavailable because one or more required types were not loaded.");
            }

            PatchOptional(
                GameTypeCatalog.FindMethod(types.SpiritLeash, "ShouldShowMark", 0),
                postfix: nameof(PatchCallbacks.GrappleMarkPostfix));
            PatchOptional(
                GameTypeCatalog.FindMethod(types.PlayerInput, "ClearControls", 0),
                postfix: nameof(PatchCallbacks.ClearControlsPostfix));

            return true;
        }
        catch (Exception exception)
        {
            _harmony.UnpatchSelf();
            _log.LogError($"Patch installation failed and was rolled back: {exception}");
            return false;
        }
    }

    private void PatchRequired(MethodInfo? original, string? prefix = null, string? postfix = null)
    {
        if (original is null)
        {
            throw new MissingMethodException("A required Ori method was not found in the generated interop assemblies.");
        }

        Patch(original, prefix, postfix);
    }

    private void PatchOptional(MethodInfo? original, string? prefix = null, string? postfix = null)
    {
        if (original is null)
        {
            return;
        }

        Patch(original, prefix, postfix);
    }

    private void Patch(MethodInfo original, string? prefix, string? postfix)
    {
        _harmony.Patch(
            original,
            prefix is null ? null : new HarmonyMethod(typeof(PatchCallbacks), prefix),
            postfix is null ? null : new HarmonyMethod(typeof(PatchCallbacks), postfix));
        _log.LogDebug($"Patched {original.DeclaringType?.FullName}.{original.Name}.");
    }
}

public static class PatchCallbacks
{
    private static GameRuntime? _runtime;

    internal static void Attach(GameRuntime runtime) => _runtime = runtime;

    public static void ButtonResultPostfix(object __instance, ref bool __result)
    {
        if (_runtime is not null)
        {
            __result = _runtime.RouteButton(__instance, __result);
        }
    }

    public static void TargetSearchPrefix(object __instance) => _runtime?.BeginTargetSearch(__instance);

    public static void TargetSearchPostfix(object __instance) => _runtime?.EndTargetSearch(__instance);

    public static void GrappleCostPostfix(object __0, float __1, float __2, bool __3, float __result) =>
        _runtime?.CaptureGrappleCandidate(__0, __1, __2, __3, __result);

    public static void BashTargetPostfix(object __instance, object? __result) =>
        _runtime?.CaptureBashTarget(__instance, __result);

    public static void DebugOverlayPostfix() => _runtime?.DrawDiagnostics();

    public static bool FaceLeftPrefix(ref bool __result)
    {
        if (_runtime is null || !_runtime.TryGetMouseFacing(out var faceLeft))
        {
            return true;
        }

        __result = faceLeft;
        return false;
    }

    public static void GrappleMarkPostfix(object __instance, ref bool __result)
    {
        if (_runtime is not null)
        {
            __result = _runtime.FilterGrappleMark(__result, __instance);
        }
    }

    public static void ClearControlsPostfix() => _runtime?.ResetInput();
}
