using System.Drawing.Drawing2D;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Monitor;

internal sealed class ScreenMapControl : Control
{
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

        DrawGrappleRanges(eventArgs.Graphics, board, scale);

        if (double.IsFinite(_frame.CursorX) && double.IsFinite(_frame.CursorY))
        {
            var center = ToClient(_frame.CursorX, _frame.CursorY, board, scale);
            var radius = (float)(_frame.EffectiveRadius * scale);
            using var radiusPen = new Pen(DiagnosticPalette.For(_frame.GrappleState), 2.0f);
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

    private void DrawGrappleRanges(Graphics graphics, RectangleF board, float scale)
    {
        if (_frame?.GrappleRangeCenterX is not { } centerX ||
            _frame.GrappleRangeCenterY is not { } centerY)
        {
            return;
        }

        var center = ToClient(centerX, centerY, board, scale);
        DrawRangeEllipse(
            graphics,
            center,
            _frame.RetainedRangeRadiusX * scale,
            _frame.RetainedRangeRadiusY * scale,
            DiagnosticPalette.RetainedRange);
        DrawRangeEllipse(
            graphics,
            center,
            _frame.NormalRangeRadiusX * scale,
            _frame.NormalRangeRadiusY * scale,
            DiagnosticPalette.OutOfRange);
    }

    private static void DrawRangeEllipse(Graphics graphics, PointF center, double radiusX, double radiusY, Color color)
    {
        if (radiusX <= 0.0 || radiusY <= 0.0)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(150, color), 1.0f) { DashStyle = DashStyle.Dash };
        graphics.DrawEllipse(
            pen,
            center.X - (float)radiusX,
            center.Y - (float)radiusY,
            (float)(radiusX * 2.0),
            (float)(radiusY * 2.0));
    }

    private void DrawMarker(
        Graphics graphics,
        DiagnosticMarker marker,
        RectangleF board,
        float scale)
    {
        var point = ToClient(marker.X, marker.Y, board, scale);
        var color = DiagnosticPalette.For(marker.State, marker.Kind);
        if (marker.Kind == "BashTarget")
        {
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, point.X - 5.0f, point.Y - 5.0f, 10.0f, 10.0f);
        }
        else
        {
            var radius = Math.Max(5.0f, (float)(_frame!.TargetMarkerRadius * scale));
            using var pen = new Pen(color, marker.Kind == "GrappleTarget" ? 3.0f : 2.0f);
            using var centerBrush = new SolidBrush(color);
            graphics.DrawEllipse(pen, point.X - radius, point.Y - radius, radius * 2.0f, radius * 2.0f);
            graphics.FillEllipse(centerBrush, point.X - 2.0f, point.Y - 2.0f, 4.0f, 4.0f);
        }

        using var textBrush = new SolidBrush(Color.WhiteSmoke);
        graphics.DrawString(marker.Label, Font, textBrush, point.X + 7.0f, point.Y - 8.0f);
    }

    private void DrawLegend(Graphics graphics, RectangleF board)
    {
        var entries = new[]
        {
            (DiagnosticPalette.Ready, "ready"),
            (DiagnosticPalette.CursorMiss, "selected / cursor miss"),
            (DiagnosticPalette.SelectorConflict, "cursor hit / selector kept another"),
            (DiagnosticPalette.RetainedRange, "retained-target range only"),
            (DiagnosticPalette.OutOfRange, "out of range"),
            (DiagnosticPalette.Blocked, "cooldown, busy, or blocked"),
            (DiagnosticPalette.Candidate, "evaluated candidate"),
            (DiagnosticPalette.Bash, "Bash target"),
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
