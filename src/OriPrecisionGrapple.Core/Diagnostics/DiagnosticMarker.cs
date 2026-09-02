namespace OriPrecisionGrapple.Core.Diagnostics;

public sealed class DiagnosticMarker
{
    public string Label { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string State { get; set; } = DiagnosticMarkerStates.Unknown;

    public string Detail { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }
}

public static class DiagnosticMarkerStates
{
    public const string Unknown = "unknown";
    public const string Ready = "ready";
    public const string CursorMiss = "cursor-miss";
    public const string SelectorConflict = "selector-conflict";
    public const string Direction = "direction";
    public const string RetainedRange = "retained-range";
    public const string OutOfRange = "out-of-range";
    public const string Cooldown = "cooldown";
    public const string Busy = "busy";
    public const string Blocked = "blocked";
    public const string Candidate = "candidate";
    public const string Bash = "bash";
}
