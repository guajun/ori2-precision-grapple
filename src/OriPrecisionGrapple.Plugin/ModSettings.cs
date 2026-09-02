using BepInEx.Configuration;

namespace OriPrecisionGrapple;

internal interface IRuntimeSettings
{
    bool Enabled { get; }
    double RadiusPixels { get; }
    int ReferenceHeight { get; }
    bool HideImpreciseMark { get; }
    bool DebugLogging { get; }
    bool ShowOverlay { get; }
    bool ShowWorldMarkers { get; }
    bool ExternalMonitorEnabled { get; }
}

internal sealed class ModSettings : IRuntimeSettings
{
    private readonly ConfigEntry<bool> _enabled;
    private readonly ConfigEntry<float> _radiusPixels;
    private readonly ConfigEntry<int> _referenceHeight;
    private readonly ConfigEntry<bool> _hideImpreciseMark;
    private readonly ConfigEntry<bool> _debugLogging;
    private readonly ConfigEntry<bool> _showOverlay;
    private readonly ConfigEntry<bool> _showWorldMarkers;
    private readonly ConfigEntry<bool> _externalMonitorEnabled;

    public ModSettings(ConfigFile config)
    {
        _enabled = config.Bind(
            "General",
            "Enabled",
            true,
            "Route shared right-click input to Grapple only when the cursor precisely hits a valid target.");
        _radiusPixels = config.Bind(
            "Precision",
            "RadiusPixelsAt1080p",
            48.0f,
            "Cursor radius around the selected Grapple point at the reference resolution.");
        _referenceHeight = config.Bind(
            "Precision",
            "ReferenceHeight",
            1080,
            "Screen height used to scale RadiusPixelsAt1080p.");
        _hideImpreciseMark = config.Bind(
            "Visuals",
            "HideImpreciseGrappleMark",
            true,
            "Hide the Grapple marker unless the cursor is within the precision radius.");
        _debugLogging = config.Bind(
            "Diagnostics",
            "DebugLogging",
            false,
            "Write routing and target details to the BepInEx log.");
        _showOverlay = config.Bind(
            "Diagnostics",
            "ShowOverlay",
            false,
            "Show the experimental in-game diagnostics overlay.");
        _showWorldMarkers = config.Bind(
            "Diagnostics",
            "ShowWorldMarkers",
            true,
            "Mark the cursor, Grapple candidates, selected target, Bash target and precision radius.");
        _externalMonitorEnabled = config.Bind(
            "Diagnostics",
            "ExternalMonitorEnabled",
            true,
            "Publish live diagnostics to OriPrecisionGrapple.Monitor over a local named pipe.");
    }

    public bool Enabled => _enabled.Value;

    public double RadiusPixels => Math.Max(0.0, _radiusPixels.Value);

    public int ReferenceHeight => Math.Max(1, _referenceHeight.Value);

    public bool HideImpreciseMark => _hideImpreciseMark.Value;

    public bool DebugLogging => _debugLogging.Value;

    public bool ShowOverlay => _showOverlay.Value;

    public bool ShowWorldMarkers => _showWorldMarkers.Value;

    public bool ExternalMonitorEnabled => _externalMonitorEnabled.Value;
}
