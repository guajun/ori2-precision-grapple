using System.Runtime.InteropServices;

namespace OriPrecisionGrapple.Runtime;

internal static class PhysicalMouse
{
    private const int VirtualKeyRightButton = 0x02;
    private const int KeyDownMask = 0x8000;

    public static bool IsRightButtonHeldByGameWindow()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
        if (foregroundProcessId != Environment.ProcessId)
        {
            return false;
        }

        return (GetAsyncKeyState(VirtualKeyRightButton) & KeyDownMask) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
