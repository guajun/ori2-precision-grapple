using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Monitor;

internal sealed class OverlayForm : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly Color TransparencyColor = Color.Magenta;
    private readonly System.Windows.Forms.Timer _placementTimer;
    private readonly Font _hudFont = new(FontFamily.GenericMonospace, 10.0f);
    private readonly Font _markerFont = new(FontFamily.GenericMonospace, 9.0f, FontStyle.Bold);
    private DiagnosticFrame? _frame;
    private bool _overlayEnabled = true;

    public OverlayForm()
    {
        Text = "Ori Precision Grapple Overlay";
        BackColor = TransparencyColor;
        TransparencyKey = TransparencyColor;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;

        _placementTimer = new System.Windows.Forms.Timer { Interval = 8 };
        _placementTimer.Tick += (_, _) => UpdatePlacement();
        Shown += (_, _) => _placementTimer.Start();
        FormClosed += (_, _) =>
        {
            _placementTimer.Stop();
            _placementTimer.Dispose();
            _hudFont.Dispose();
            _markerFont.Dispose();
        };
    }

    public bool OverlayEnabled
    {
        get => _overlayEnabled;
        set
        {
            _overlayEnabled = value;
            UpdatePlacement();
        }
    }

    public bool ShowHud { get; set; } = true;

    public DiagnosticFrame? Frame
    {
        get => _frame;
        set
        {
            _frame = value;
            Invalidate();
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExLayered | WsExTransparent | WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (_frame is null || _frame.ScreenWidth <= 0 || _frame.ScreenHeight <= 0)
        {
            return;
        }

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        eventArgs.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        var scaleX = (double)ClientSize.Width / _frame.ScreenWidth;
        var scaleY = (double)ClientSize.Height / _frame.ScreenHeight;

        DrawGrappleRanges(eventArgs.Graphics, scaleX, scaleY);

        if (double.IsFinite(_frame.CursorX) && double.IsFinite(_frame.CursorY))
        {
            var center = ToClient(_frame.CursorX, _frame.CursorY, scaleX, scaleY);
            var radiusX = (float)(_frame.EffectiveRadius * scaleX);
            var radiusY = (float)(_frame.EffectiveRadius * scaleY);
            using var radiusPen = new Pen(DiagnosticPalette.For(_frame.GrappleState), 2.5f);
            eventArgs.Graphics.DrawEllipse(
                radiusPen,
                center.X - radiusX,
                center.Y - radiusY,
                radiusX * 2.0f,
                radiusY * 2.0f);
        }

        foreach (var marker in _frame.Markers)
        {
            DrawMarker(eventArgs.Graphics, marker, scaleX, scaleY);
        }

        if (double.IsFinite(_frame.CursorX) && double.IsFinite(_frame.CursorY))
        {
            var cursor = ToClient(_frame.CursorX, _frame.CursorY, scaleX, scaleY);
            using var cursorPen = new Pen(Color.WhiteSmoke, 1.75f);
            eventArgs.Graphics.DrawLine(cursorPen, cursor.X - 9, cursor.Y, cursor.X + 9, cursor.Y);
            eventArgs.Graphics.DrawLine(cursorPen, cursor.X, cursor.Y - 9, cursor.X, cursor.Y + 9);
        }

        if (ShowHud && _frame.Lines.Length > 0)
        {
            DrawHud(eventArgs.Graphics, _frame.Lines);
        }
    }

    private void DrawGrappleRanges(Graphics graphics, double scaleX, double scaleY)
    {
        if (_frame?.GrappleRangeCenterX is not { } centerX ||
            _frame.GrappleRangeCenterY is not { } centerY)
        {
            return;
        }

        var center = ToClient(centerX, centerY, scaleX, scaleY);
        DrawRangeEllipse(
            graphics,
            center,
            _frame.RetainedRangeRadiusX * scaleX,
            _frame.RetainedRangeRadiusY * scaleY,
            DiagnosticPalette.RetainedRange);
        DrawRangeEllipse(
            graphics,
            center,
            _frame.NormalRangeRadiusX * scaleX,
            _frame.NormalRangeRadiusY * scaleY,
            DiagnosticPalette.OutOfRange);
    }

    private static void DrawRangeEllipse(Graphics graphics, PointF center, double radiusX, double radiusY, Color color)
    {
        if (radiusX <= 0.0 || radiusY <= 0.0)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(150, color), 1.5f) { DashStyle = DashStyle.Dash };
        graphics.DrawEllipse(
            pen,
            center.X - (float)radiusX,
            center.Y - (float)radiusY,
            (float)(radiusX * 2.0),
            (float)(radiusY * 2.0));
    }

    private void DrawHud(Graphics graphics, IReadOnlyList<string> lines)
    {
        const float x = 12.0f;
        const float y = 12.0f;
        const float lineHeight = 18.0f;
        var contentWidth = lines.Max(line => graphics.MeasureString(line, _hudFont).Width);
        var width = Math.Clamp(contentWidth + 22.0f, 700.0f, 1100.0f);
        var height = 16.0f + (lines.Count * lineHeight);
        using var background = new SolidBrush(Color.FromArgb(24, 27, 30));
        using var foreground = new SolidBrush(Color.WhiteSmoke);
        graphics.FillRectangle(background, x, y, width, height);
        for (var index = 0; index < lines.Count; index++)
        {
            graphics.DrawString(lines[index], _hudFont, foreground, x + 8.0f, y + 7.0f + (index * lineHeight));
        }
    }

    private void DrawMarker(Graphics graphics, DiagnosticMarker marker, double scaleX, double scaleY)
    {
        var point = ToClient(marker.X, marker.Y, scaleX, scaleY);
        var color = DiagnosticPalette.For(marker.State, marker.Kind);
        if (marker.Kind == "BashTarget")
        {
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, point.X - 5.0f, point.Y - 5.0f, 10.0f, 10.0f);
        }
        else
        {
            var radiusX = Math.Max(7.0f, (float)(_frame!.TargetMarkerRadius * scaleX));
            var radiusY = Math.Max(7.0f, (float)(_frame.TargetMarkerRadius * scaleY));
            using var ringPen = new Pen(color, marker.Kind == "GrappleTarget" ? 3.0f : 2.0f);
            using var centerBrush = new SolidBrush(color);
            graphics.DrawEllipse(ringPen, point.X - radiusX, point.Y - radiusY, radiusX * 2.0f, radiusY * 2.0f);
            graphics.FillEllipse(centerBrush, point.X - 2.5f, point.Y - 2.5f, 5.0f, 5.0f);
        }

        DrawOutlinedText(graphics, marker.Label, _markerFont, color, point.X + 12.0f, point.Y - 14.0f);
        if (!string.IsNullOrWhiteSpace(marker.Detail))
        {
            DrawOutlinedText(graphics, marker.Detail, _hudFont, color, point.X + 12.0f, point.Y + 2.0f);
        }
    }

    private static void DrawOutlinedText(Graphics graphics, string text, Font font, Color color, float x, float y)
    {
        using var shadow = new SolidBrush(Color.Black);
        using var foreground = new SolidBrush(color);
        graphics.DrawString(text, font, shadow, x + 1.0f, y + 1.0f);
        graphics.DrawString(text, font, foreground, x, y);
    }

    private PointF ToClient(double x, double y, double scaleX, double scaleY) =>
        new((float)(x * scaleX), (float)(ClientSize.Height - (y * scaleY)));

    private void UpdatePlacement()
    {
        if (!_overlayEnabled || !TryGetGameClientBounds(out var gameWindow, out var bounds) || GetForegroundWindow() != gameWindow)
        {
            if (Visible)
            {
                Hide();
            }

            return;
        }

        if (Bounds != bounds)
        {
            SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        if (!Visible)
        {
            Show();
        }

        SetWindowPos(Handle, HwndTopmost, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);
    }

    private static bool TryGetGameClientBounds(out IntPtr window, out Rectangle bounds)
    {
        window = IntPtr.Zero;
        bounds = Rectangle.Empty;
        foreach (var process in Process.GetProcessesByName("oriwotw"))
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero && !IsIconic(process.MainWindowHandle))
                {
                    window = process.MainWindowHandle;
                    break;
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        if (window == IntPtr.Zero || !GetClientRect(window, out var clientRect))
        {
            return false;
        }

        var topLeft = new NativePoint();
        if (!ClientToScreen(window, ref topLeft))
        {
            return false;
        }

        var width = clientRect.Right - clientRect.Left;
        var height = clientRect.Bottom - clientRect.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        bounds = new Rectangle(topLeft.X, topLeft.Y, width, height);
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
