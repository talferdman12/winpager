using WinPager.Net;

namespace WinPager.UI;

/// <summary>
/// The tray fly-out: one big button per desk. Click a button, that person gets paged.
/// Closes itself as soon as it loses focus, like a real tray popup.
/// </summary>
public sealed class PagerPopup : Form
{
    private const int ButtonHeight = 46;
    private const int Padding_ = 10;
    private const int PopupWidth = 280;

    private readonly PeerService _service;
    private readonly Config _config;
    private readonly FlowLayoutPanel _list;
    private readonly Label _emptyLabel;
    private readonly TextBox _noteBox;
    private readonly Label _statusLabel;
    private DateTime _hiddenAtUtc = DateTime.MinValue;

    public PagerPopup(PeerService service, Config config)
    {
        _service = service;
        _config = config;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Width = PopupWidth;
        KeyPreview = true;
        TopMost = true;

        var header = new Label
        {
            Text = "Page someone",
            Font = Theme.TitleFont,
            ForeColor = Theme.Text,
            AutoSize = false,
            Height = 34,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(Padding_, 0, 0, 0),
        };

        _list = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(Padding_, 0, Padding_, 0),
            BackColor = Theme.Background,
        };

        _emptyLabel = new Label
        {
            Text = "No other desks online yet.\nMake sure the app is running there\nand both PCs are on the same network.",
            Font = Theme.SmallFont,
            ForeColor = Theme.TextMuted,
            AutoSize = false,
            Height = 70,
            Width = PopupWidth - (Padding_ * 2),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
        };

        _noteBox = new TextBox
        {
            PlaceholderText = "Optional note (e.g. call on line 2)",
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.SmallFont,
            Width = PopupWidth - (Padding_ * 2),
            Margin = new Padding(Padding_, 4, Padding_, 4),
        };

        _statusLabel = new Label
        {
            Text = $"You are \"{config.DisplayName}\"",
            Font = Theme.SmallFont,
            ForeColor = Theme.TextMuted,
            AutoSize = false,
            Height = 24,
            Dock = DockStyle.Bottom,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var container = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Background,
        };
        bottom.Controls.Add(_noteBox);

        container.Controls.Add(_list);
        Controls.Add(container);
        Controls.Add(bottom);
        Controls.Add(_statusLabel);
        Controls.Add(header);

        _list.Controls.Add(_emptyLabel);

        _service.PeersChanged += OnPeersChanged;
        KeyDown += OnKeyDown;
    }

    /// <summary>How long ago this popup was last hidden. Lets the tray ignore the
    /// click that closed it, instead of treating it as a request to reopen.</summary>
    public double MillisecondsSinceHidden =>
        (DateTime.UtcNow - _hiddenAtUtc).TotalMilliseconds;

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible) _hiddenAtUtc = DateTime.UtcNow;
        base.OnVisibleChanged(e);
    }

    /// <summary>Show the popup anchored to the notification area on the active screen.</summary>
    public void ShowNear()
    {
        Rebuild(_service.Peers);
        _statusLabel.Text = $"You are \"{_config.DisplayName}\"";
        _noteBox.Clear();

        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            area.Right - Width - 12,
            area.Bottom - Height - 12);

        Show();
        Activate();
        BringToFront();
    }

    private void OnPeersChanged(IReadOnlyList<Peer> peers)
    {
        if (IsDisposed) return;
        if (Visible) Rebuild(peers);
    }

    private void Rebuild(IReadOnlyList<Peer> peers)
    {
        _list.SuspendLayout();

        foreach (var control in _list.Controls.OfType<Control>().Where(c => c != _emptyLabel).ToList())
        {
            _list.Controls.Remove(control);
            control.Dispose();
        }

        _emptyLabel.Visible = peers.Count == 0;

        foreach (var peer in peers)
            _list.Controls.Add(CreatePeerButton(peer));

        _list.ResumeLayout();

        // Height = header + list + note + status, clamped so a big office still fits.
        var listHeight = Math.Min(_list.PreferredSize.Height, 420);
        Height = 34 + listHeight + 40 + 24 + Padding_;
    }

    private Button CreatePeerButton(Peer peer)
    {
        var button = new Button
        {
            Text = peer.Name,
            Width = PopupWidth - (Padding_ * 2) - 2,
            Height = ButtonHeight,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.ButtonFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 6),
            Cursor = Cursors.Hand,
            Tag = peer.Id,
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = Theme.Accent;
        button.Click += (_, _) => PagePeer(peer);

        return button;
    }

    private void PagePeer(Peer peer)
    {
        var note = _noteBox.Text;
        var sent = _service.SendPage(peer.Id, note);

        if (!sent)
        {
            MessageBox.Show(
                this,
                $"{peer.Name} just went offline. Nothing was sent.",
                "WinPager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Hide();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            Hide();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The tray owns this window's lifetime; the X and Alt+F4 just hide it.
        if (e.CloseReason is CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _service.PeersChanged -= OnPeersChanged;

        base.Dispose(disposing);
    }
}
