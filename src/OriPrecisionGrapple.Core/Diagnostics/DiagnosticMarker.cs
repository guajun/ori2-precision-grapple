namespace OriPrecisionGrapple.Core.Diagnostics;

public sealed class DiagnosticMarker
{
    public string Label { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }
}
