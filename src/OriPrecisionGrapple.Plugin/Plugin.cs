using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using OriPrecisionGrapple.Runtime;

namespace OriPrecisionGrapple;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("oriwotw.exe")]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "io.github.guajun.ori2precisiongrapple";
    public const string PluginName = "Ori Precision Grapple";
    public const string PluginVersion = "0.6.0";

    private Harmony? _harmony;
    private GameRuntime? _runtime;

    public override void Load()
    {
        var settings = new ModSettings(Config);
        _runtime = new GameRuntime(settings, Log);
        _harmony = new Harmony(PluginGuid);
        var installer = new GamePatchInstaller(_harmony, _runtime, Log);

        if (!installer.TryInstall())
        {
            Log.LogError(
                "Required Ori IL2CPP types were not found. Complete one BepInEx first run, then inspect LogOutput.log.");
            return;
        }

        Log.LogInfo($"{PluginName} {PluginVersion} loaded. Right-click routing is active.");
    }

    public override bool Unload()
    {
        _runtime?.Dispose();
        _runtime = null;
        _harmony?.UnpatchSelf();
        _harmony = null;
        return true;
    }
}
