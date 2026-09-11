using System.Media;
using ChabadOfficePager.Net;

namespace ChabadOfficePager.UI;

/// <summary>
/// The on-screen alert shown to the person being paged. Sits on top of everything,
/// says who paged them, and clears itself after a few seconds.
/// </summary>
public sealed class AlertForm : Form
{
    private static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(25);

    private readonly System.Windows.Forms.Timer _dismissTimer;

    public AlertForm(PagerMessage page, bool playSound)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Theme.Alert;
        Width = 380;
        Height = string.IsNullOrWhiteSpace(page.Text) ? 130 : 175;

        var title = new Label
        {
            Text = "📟  You're being paged",
            Font = Theme.SmallFont,
            ForeColor = Color.FromArgb(255, 220, 220),
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var who = new Label
        {
            Text = page.SenderName,
            Font = Theme.AlertFont,
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 44,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var note = new Label
        {
            Text = page.Text ?? "",
            Font = Theme.TitleFont,
            ForeColor = Color.FromArgb(255, 235, 235),
            Dock = DockStyle.Top,
            Height = string.IsNullOrWhiteSpace(page.Text) ? 0 : 44,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = !string.IsNullOrWhiteSpace(page.Text),
        };

        var dismiss = new Label
        {
            Text = "Click anywhere to dismiss",
            Font = Theme.SmallFont,
            ForeColor = Color.FromArgb(255, 200, 200),
            Dock = DockStyle.Bottom,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        Controls.Add(dismiss);
        Controls.Add(note);
        Controls.Add(who);
        Controls.Add(title);

        // Any click, anywhere on the alert, closes it.
        Click += (_, _) => Close();
        foreach (Control child in Controls)
            child.Click += (_, _) => Close();

        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        Location = new Point(
            area.Right - Width - 16,
            area.Bottom - Height - 16);

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

    /// <summary>Show without stealing keyboard focus from whatever the user is typing in.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dismissTimer.Dispose();

        base.Dispose(disposing);
    }
}
