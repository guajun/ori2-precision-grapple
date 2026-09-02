namespace OriPrecisionGrapple.Core.Diagnostics;

public sealed class DiagnosticFrame
{
    public long Sequence { get; set; }

    public long TimestampUnixMilliseconds { get; set; }

    public int ScreenWidth { get; set; }

    public int ScreenHeight { get; set; }

    public double CursorX { get; set; }

    public double CursorY { get; set; }

    public double EffectiveRadius { get; set; }

    public bool PrecisionHit { get; set; }

    public double? GrappleTargetX { get; set; }

    public double? GrappleTargetY { get; set; }

    public string[] Lines { get; set; } = Array.Empty<string>();

    public DiagnosticMarker[] Markers { get; set; } = Array.Empty<DiagnosticMarker>();
}
