using OriPrecisionBash.Core;

namespace OriPrecisionBash.Runtime;

internal enum DebugMarkerKind
{
    GrappleCandidate,
    GrappleTarget,
    BashTarget,
}

internal readonly struct DebugMarker
{
    public DebugMarker(ScreenPoint point, string label, DebugMarkerKind kind)
    {
        Point = point;
        Label = label;
        Kind = kind;
    }

    public ScreenPoint Point { get; }

    public string Label { get; }

    public DebugMarkerKind Kind { get; }
}

internal sealed class DebugSnapshot
{
    public int ScreenWidth { get; init; }

    public int ScreenHeight { get; init; }

    public ScreenPoint Cursor { get; init; }

    public ScreenPoint? GrappleTarget { get; init; }

    public double EffectiveRadius { get; init; }

    public bool PrecisionHit { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    public IReadOnlyList<DebugMarker> Markers { get; init; } = Array.Empty<DebugMarker>();
}
