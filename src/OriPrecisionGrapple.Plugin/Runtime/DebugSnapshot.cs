using OriPrecisionGrapple.Core;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Runtime;

internal enum DebugMarkerKind
{
    GrappleCandidate,
    GrappleTarget,
    BashTarget,
}

internal readonly struct DebugMarker
{
    public DebugMarker(
        ScreenPoint point,
        string label,
        DebugMarkerKind kind,
        string state = DiagnosticMarkerStates.Unknown,
        string detail = "")
    {
        Point = point;
        Label = label;
        Kind = kind;
        State = state;
        Detail = detail;
    }

    public ScreenPoint Point { get; }

    public string Label { get; }

    public DebugMarkerKind Kind { get; }

    public string State { get; }

    public string Detail { get; }
}

internal sealed class DebugSnapshot
{
    public int ScreenWidth { get; init; }

    public int ScreenHeight { get; init; }

    public ScreenPoint Cursor { get; init; }

    public ScreenPoint? GrappleTarget { get; init; }

    public double EffectiveRadius { get; init; }

    public double TargetMarkerRadius { get; init; }

    public bool PrecisionHit { get; init; }

    public string GrappleState { get; init; } = DiagnosticMarkerStates.Unknown;

    public ScreenPoint? GrappleRangeCenter { get; init; }

    public double NormalRangeRadiusX { get; init; }

    public double NormalRangeRadiusY { get; init; }

    public double RetainedRangeRadiusX { get; init; }

    public double RetainedRangeRadiusY { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    public IReadOnlyList<DebugMarker> Markers { get; init; } = Array.Empty<DebugMarker>();
}
