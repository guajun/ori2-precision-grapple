using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Monitor;

internal sealed class MainForm : Form
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ScreenMapControl _screenMap = new();
    private readonly RichTextBox _details = new();
    private readonly ToolStripStatusLabel _connectionStatus = new("Waiting for Ori...");
    private readonly ToolStripStatusLabel _frameStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };

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
            Panel2MinSize = 460,
            SplitterDistance = 720,
        };
        split.Panel1.Controls.Add(_screenMap);
        split.Panel2.Controls.Add(_details);

        var status = new StatusStrip();
        status.Items.Add(_connectionStatus);
        status.Items.Add(_frameStatus);

        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(status);
        Shown += OnShown;
        FormClosed += (_, _) => _cancellation.Cancel();
    }

    private async void OnShown(object? sender, EventArgs eventArgs)
    {
        var client = new PipeMonitorClient();
        await client.RunAsync(ReceiveFrame, SetConnectionStatus, _cancellation.Token);
    }

    private void ReceiveFrame(DiagnosticFrame frame)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            _screenMap.Frame = frame;
            _details.Text = string.Join(Environment.NewLine, frame.Lines);
            var age = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - frame.TimestampUnixMilliseconds);
            _frameStatus.Text = $"Frame #{frame.Sequence}  |  {frame.ScreenWidth}x{frame.ScreenHeight}  |  {age} ms";
        });
    }

    private void SetConnectionStatus(string status)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() => _connectionStatus.Text = status);
    }
}
