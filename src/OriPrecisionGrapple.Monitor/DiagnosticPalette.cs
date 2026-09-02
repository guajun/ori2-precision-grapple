using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Monitor;

internal static class DiagnosticPalette
{
    public static readonly Color Ready = Color.FromArgb(68, 214, 112);
    public static readonly Color CursorMiss = Color.FromArgb(235, 77, 75);
    public static readonly Color SelectorConflict = Color.FromArgb(255, 213, 79);
    public static readonly Color Direction = Color.FromArgb(255, 145, 77);
    public static readonly Color RetainedRange = Color.FromArgb(183, 128, 255);
    public static readonly Color OutOfRange = Color.FromArgb(150, 160, 170);
    public static readonly Color Blocked = Color.FromArgb(222, 105, 220);
    public static readonly Color Candidate = Color.FromArgb(52, 202, 235);
    public static readonly Color Bash = Color.FromArgb(245, 166, 35);

    public static Color For(string state, string kind = "") => state switch
    {
        DiagnosticMarkerStates.Ready => Ready,
        DiagnosticMarkerStates.CursorMiss => CursorMiss,
        DiagnosticMarkerStates.SelectorConflict => SelectorConflict,
        DiagnosticMarkerStates.Direction => Direction,
        DiagnosticMarkerStates.RetainedRange => RetainedRange,
        DiagnosticMarkerStates.OutOfRange => OutOfRange,
        DiagnosticMarkerStates.Cooldown or
        DiagnosticMarkerStates.Busy or
        DiagnosticMarkerStates.Blocked => Blocked,
        DiagnosticMarkerStates.Bash => Bash,
        _ when kind == "BashTarget" => Bash,
        _ => Candidate,
    };
}
