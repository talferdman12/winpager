using System.Media;
using WinPager.Net;

namespace WinPager.UI;

/// <summary>
/// The on-screen alert shown to the person being paged. Sits on top of everything,
/// says who paged them, and clears itself after a few seconds.
/// </summary>
public sealed class AlertForm : Form
{
    private static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(25);

    private readonly System.Windows.Forms.Timer _dismissTimer;
    private readonly TableLayoutPanel _root;

    public AlertForm(PagerMessage page, bool playSound)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Theme.Alert;
        AutoScaleMode = AutoScaleMode.Font;

        _root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            BackColor = Theme.Alert,
            Padding = new Padding(Scale(18), Scale(14), Scale(18), Scale(14)),
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _root.Controls.Add(MakeLabel(
            "You're being paged", Theme.SmallFont, Color.FromArgb(255, 214, 214)));
        _root.Controls.Add(MakeLabel(
            page.SenderName, Theme.AlertFont, Color.White));

        if (!string.IsNullOrWhiteSpace(page.Text))
        {
            _root.Controls.Add(MakeLabel(
                page.Text, Theme.TitleFont, Color.FromArgb(255, 236, 236)));
        }

        _root.Controls.Add(MakeLabel(
            "Click to dismiss", Theme.SmallFont, Color.FromArgb(255, 196, 196)));

        Controls.Add(_root);

        // A click anywhere on the alert, including on any label, closes it.
        Click += (_, _) => Close();
        AddClickToClose(this);

        _dismissTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)AutoDismissAfter.TotalMilliseconds,
        };
        _dismissTimer.Tick += (_, _) => Close();
        _dismissTimer.Start();

        if (playSound)
        {
            try { SystemSounds.Exclamation.Play(); }
            catch (Exception ex) { Log.Write($"Alert sound failed: {ex.Message}"); }
        }
    }

    private int Scale(int logicalPixels) => LogicalToDeviceUnits(logicalPixels);

    private Label MakeLabel(string text, Font font, Color color) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        BackColor = Theme.Alert,
        AutoSize = true,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Margin = new Padding(0, Scale(2), 0, Scale(2)),
        MaximumSize = new Size(Scale(340), 0),
    };

    private void AddClickToClose(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            child.Click += (_, _) => Close();
            AddClickToClose(child);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _root.Width = Scale(360);
        ClientSize = new Size(_root.Width, _root.PreferredSize.Height);

        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        Location = new Point(
            area.Right - Width - Scale(16),
            area.Bottom - Height - Scale(16));
    }

    /// <summary>Show without stealing keyboard focus from whatever the user is typing in.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var pen = new Pen(Color.FromArgb(255, 120, 120));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dismissTimer.Dispose();

        base.Dispose(disposing);
    }
}
