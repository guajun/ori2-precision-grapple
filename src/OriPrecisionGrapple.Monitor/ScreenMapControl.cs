using System.Drawing.Drawing2D;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Monitor;

internal sealed class ScreenMapControl : Control
{
    private static readonly Color GrappleCandidateColor = Color.FromArgb(52, 202, 235);
    private static readonly Color GrappleTargetColor = Color.FromArgb(68, 214, 112);
    private static readonly Color BashTargetColor = Color.FromArgb(245, 166, 35);

    private DiagnosticFrame? _frame;

    public ScreenMapControl()
    {
        BackColor = Color.FromArgb(15, 18, 20);
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    public DiagnosticFrame? Frame
    {
        get => _frame;
        set
        {
            _frame = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_frame is null || _frame.ScreenWidth <= 0 || _frame.ScreenHeight <= 0)
        {
            DrawCenteredMessage(eventArgs.Graphics, "Waiting for Ori diagnostic frames");
            return;
        }

        const float margin = 28.0f;
        var availableWidth = Math.Max(1.0f, ClientSize.Width - (margin * 2.0f));
        var availableHeight = Math.Max(1.0f, ClientSize.Height - (margin * 2.0f));
        var scale = Math.Min(availableWidth / _frame.ScreenWidth, availableHeight / _frame.ScreenHeight);
        var boardWidth = _frame.ScreenWidth * scale;
        var boardHeight = _frame.ScreenHeight * scale;
        var originX = (ClientSize.Width - boardWidth) / 2.0f;
        var originY = (ClientSize.Height - boardHeight) / 2.0f;
        var board = new RectangleF(originX, originY, boardWidth, boardHeight);

        using var boardBrush = new SolidBrush(Color.FromArgb(30, 35, 39));
        using var boardPen = new Pen(Color.FromArgb(92, 102, 110), 1.0f);
        eventArgs.Graphics.FillRectangle(boardBrush, board);
        eventArgs.Graphics.DrawRectangle(boardPen, board.X, board.Y, board.Width, board.Height);

        if (_frame.GrappleTargetX.HasValue && _frame.GrappleTargetY.HasValue)
        {
            var center = ToClient(_frame.GrappleTargetX.Value, _frame.GrappleTargetY.Value, board, scale);
            var radius = (float)(_frame.EffectiveRadius * scale);
            using var radiusPen = new Pen(
                _frame.PrecisionHit ? GrappleTargetColor : Color.FromArgb(235, 77, 75),
                2.0f);
            eventArgs.Graphics.DrawEllipse(radiusPen, center.X - radius, center.Y - radius, radius * 2.0f, radius * 2.0f);
        }

        foreach (var marker in _frame.Markers)
        {
            DrawMarker(eventArgs.Graphics, marker, board, scale);
        }

        if (double.IsFinite(_frame.CursorX) && double.IsFinite(_frame.CursorY))
        {
            var cursor = ToClient(_frame.CursorX, _frame.CursorY, board, scale);
            using var cursorPen = new Pen(Color.WhiteSmoke, 1.5f);
            eventArgs.Graphics.DrawLine(cursorPen, cursor.X - 8, cursor.Y, cursor.X + 8, cursor.Y);
            eventArgs.Graphics.DrawLine(cursorPen, cursor.X, cursor.Y - 8, cursor.X, cursor.Y + 8);
        }

        DrawLegend(eventArgs.Graphics, board);
    }

    private void DrawMarker(
        Graphics graphics,
        DiagnosticMarker marker,
        RectangleF board,
        float scale)
    {
        var point = ToClient(marker.X, marker.Y, board, scale);
        var color = marker.Kind switch
        {
            "GrappleTarget" => GrappleTargetColor,
            "BashTarget" => BashTargetColor,
            _ => GrappleCandidateColor,
        };

        using var brush = new SolidBrush(color);
        using var textBrush = new SolidBrush(Color.WhiteSmoke);
        graphics.FillEllipse(brush, point.X - 5.0f, point.Y - 5.0f, 10.0f, 10.0f);
        graphics.DrawString(marker.Label, Font, textBrush, point.X + 7.0f, point.Y - 8.0f);
    }

    private void DrawLegend(Graphics graphics, RectangleF board)
    {
        var entries = new[]
        {
            (GrappleTargetColor, "G* selected Grapple"),
            (GrappleCandidateColor, "G# evaluated Grapple"),
            (BashTargetColor, "B Bash target"),
        };
        var y = board.Top + 10.0f;
        foreach (var entry in entries)
        {
            using var markerBrush = new SolidBrush(entry.Item1);
            using var textBrush = new SolidBrush(Color.Gainsboro);
            graphics.FillEllipse(markerBrush, board.Left + 10.0f, y + 3.0f, 9.0f, 9.0f);
            graphics.DrawString(entry.Item2, Font, textBrush, board.Left + 25.0f, y);
            y += 19.0f;
        }
    }

    private void DrawCenteredMessage(Graphics graphics, string text)
    {
        using var brush = new SolidBrush(Color.FromArgb(180, 190, 198));
        var size = graphics.MeasureString(text, Font);
        graphics.DrawString(
            text,
            Font,
            brush,
            (ClientSize.Width - size.Width) / 2.0f,
            (ClientSize.Height - size.Height) / 2.0f);
    }

    private static PointF ToClient(double x, double y, RectangleF board, float scale) =>
        new(
            board.Left + ((float)x * scale),
            board.Bottom - ((float)y * scale));
}
