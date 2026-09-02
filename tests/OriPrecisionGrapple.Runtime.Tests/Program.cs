using BepInEx.Logging;
using HarmonyLib;
using OriPrecisionGrapple;
using OriPrecisionGrapple.Runtime;

var rightButtonHeld = true;
var settings = new FakeSettings();
var log = new ManualLogSource("OriPrecisionGrapple.Runtime.Tests");
var runtime = new GameRuntime(settings, log, () => rightButtonHeld);
var harmony = new Harmony("io.github.guajun.ori2precisiongrapple.runtime-tests");
var installer = new GamePatchInstaller(harmony, runtime, log);

try
{
    True(installer.TryInstall(), "runtime patches install against the fake game API");

    var leash = new SeinSpiritLeashAbility();
    leash.FindClosestAttackHandler();
    Near(1.0f, leash.LastInputDirection.x, "target search uses mouse X direction");
    Near(0.0f, leash.LastInputDirection.y, "target search uses mouse Y direction");
    False(leash.FaceLeftDuringSearch, "facing follows the mouse during target search");
    Near(0.0f, Core.Input.Horizontal, "horizontal input is restored after target search");
    Near(1.0f, Core.Input.Vertical, "vertical input is restored after target search");

    False(PlayerInput.Instance.Bash.GetValue(), "precise target suppresses Bash");
    True(PlayerInput.Instance.Grapple.GetValue(), "precise target enables Grapple");
    True(leash.ShouldShowMark(), "precise target keeps the Grapple mark");
    new GameController().OnGUI();
    True(UnityEngine.GUI.BoxCalls > 0, "diagnostics overlay draws framed content");
    True(UnityEngine.GUI.LabelCalls > 0, "diagnostics overlay draws condition labels");

    rightButtonHeld = false;
    False(PlayerInput.Instance.Bash.GetValue(), "release preserves original Bash input");
    MoonInput.MousePosition = new UnityEngine.Vector3(200, 100, 1);
    leash.m_targetLeash = new FakeLeashableInfo
    {
        SurfaceWorldPos = new UnityEngine.Vector3(300, 100, 1),
    };
    leash.FindClosestAttackHandler();
    False(leash.ShouldShowMark(), "imprecise target hides the Grapple mark");

    rightButtonHeld = true;
    True(PlayerInput.Instance.Bash.GetValue(), "imprecise target enables Bash");
    False(PlayerInput.Instance.Grapple.GetValue(), "imprecise target suppresses Grapple");

    Console.WriteLine("PASS runtime Harmony integration");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL runtime Harmony integration: {exception}");
    return 1;
}
finally
{
    harmony.UnpatchSelf();
}

static void True(bool value, string message)
{
    if (!value)
    {
        throw new InvalidOperationException($"Expected true: {message}.");
    }
}

static void False(bool value, string message) => True(!value, message);

static void Near(float expected, float actual, string message)
{
    if (Math.Abs(expected - actual) > 0.001f)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}: {message}.");
    }
}

internal sealed class FakeSettings : IRuntimeSettings
{
    public bool Enabled => true;
    public double RadiusPixels => 24.0;
    public int ReferenceHeight => 1080;
    public bool HideImpreciseMark => true;
    public bool DebugLogging => false;
    public bool ShowOverlay => true;
    public bool ShowWorldMarkers => true;
}
