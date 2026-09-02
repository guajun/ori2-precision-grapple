using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Monitor;

internal sealed class MainForm : Form
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ScreenMapControl _screenMap = new();
    private readonly OverlayForm _overlay = new();
    private readonly RichTextBox _details = new();
    private readonly ToolStripStatusLabel _connectionStatus = new("Waiting for Ori...");
    private readonly ToolStripStatusLabel _frameStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private DiagnosticFrame? _pendingFrame;
    private int _frameDispatchQueued;
    private long _previousFrameTimestamp;
    private double _smoothedRate;

    public MainForm()
    {
        Text = "Ori Precision Grapple Monitor";
        ClientSize = new Size(1220, 720);
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10, 8, 10, 4),
            WrapContents = false,
        };
        toolbar.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold),
            Text = "Ori Precision Grapple Diagnostics",
            Margin = new Padding(0, 3, 22, 0),
        });
        var alwaysOnTop = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = "Always on top",
            Margin = new Padding(0, 2, 0, 0),
        };
        alwaysOnTop.CheckedChanged += (_, _) => TopMost = alwaysOnTop.Checked;
        toolbar.Controls.Add(alwaysOnTop);
        var overlayEnabled = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = "Overlay on Ori",
            Margin = new Padding(18, 2, 0, 0),
        };
        overlayEnabled.CheckedChanged += (_, _) => _overlay.OverlayEnabled = overlayEnabled.Checked;
        toolbar.Controls.Add(overlayEnabled);
        var overlayHud = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = "Overlay HUD",
            Margin = new Padding(18, 2, 0, 0),
        };
        overlayHud.CheckedChanged += (_, _) => _overlay.ShowHud = overlayHud.Checked;
        toolbar.Controls.Add(overlayHud);

        _details.BackColor = Color.FromArgb(22, 25, 28);
        _details.BorderStyle = BorderStyle.None;
        _details.Dock = DockStyle.Fill;
        _details.Font = new Font(FontFamily.GenericMonospace, 10.0f);
        _details.ForeColor = Color.Gainsboro;
        _details.ReadOnly = true;
        _details.Text = "Waiting for diagnostic frames from Ori...";
        _details.WordWrap = false;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
        };
        split.Panel1.Controls.Add(_screenMap);
        split.Panel2.Controls.Add(_details);

        var status = new StatusStrip();
        status.Items.Add(_connectionStatus);
        status.Items.Add(_frameStatus);

        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(status);
        split.Panel1MinSize = 320;
        split.Panel2MinSize = 420;
        split.SplitterDistance = Math.Max(split.Panel1MinSize, split.Width - 480);
        Shown += OnShown;
        FormClosed += (_, _) =>
        {
            _cancellation.Cancel();
            _overlay.Close();
        };
    }

    private async void OnShown(object? sender, EventArgs eventArgs)
    {
        _overlay.Show();
        var client = new PipeMonitorClient();
        await client.RunAsync(ReceiveFrame, SetConnectionStatus, _cancellation.Token);
    }

    private void ReceiveFrame(DiagnosticFrame frame)
    {
        if (IsDisposed)
        {
            return;
        }

        Interlocked.Exchange(ref _pendingFrame, frame);
        if (Interlocked.Exchange(ref _frameDispatchQueued, 1) == 0)
        {
            BeginInvoke(ApplyPendingFrame);
        }
    }

    private void ApplyPendingFrame()
    {
        var frame = Interlocked.Exchange(ref _pendingFrame, null);
        Volatile.Write(ref _frameDispatchQueued, 0);
        if (frame is null)
        {
            return;
        }

        _screenMap.Frame = frame;
        _overlay.Frame = frame;
        _details.Text = string.Join(Environment.NewLine, frame.Lines);
        var age = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - frame.TimestampUnixMilliseconds);
        if (_previousFrameTimestamp > 0 && frame.TimestampUnixMilliseconds > _previousFrameTimestamp)
        {
            var instantRate = 1000.0 / (frame.TimestampUnixMilliseconds - _previousFrameTimestamp);
            _smoothedRate = _smoothedRate <= 0 ? instantRate : (_smoothedRate * 0.85) + (instantRate * 0.15);
        }

        _previousFrameTimestamp = frame.TimestampUnixMilliseconds;
        _frameStatus.Text = $"Frame #{frame.Sequence}  |  {_smoothedRate:F1} Hz  |  {frame.ScreenWidth}x{frame.ScreenHeight}  |  {age} ms";
        Text = $"Ori Precision Grapple Monitor - Connected - {_smoothedRate:F1} Hz";

        if (Volatile.Read(ref _pendingFrame) is not null && Interlocked.Exchange(ref _frameDispatchQueued, 1) == 0)
        {
            BeginInvoke(ApplyPendingFrame);
        }
    }

    private void SetConnectionStatus(string status)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            _connectionStatus.Text = status;
            Text = $"Ori Precision Grapple Monitor - {status}";
        });
    }
}
