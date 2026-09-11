using WinPager.Net;

namespace WinPager.UI;

/// <summary>
/// The tray fly-out: one big button per desk. Click a button, that person gets paged.
/// Closes itself as soon as it loses focus, like a real tray popup.
/// </summary>
public sealed class PagerPopup : Form
{
    private readonly PeerService _service;
    private readonly Config _config;
    private readonly TableLayoutPanel _root;
    private readonly Panel _listHost;
    private readonly TableLayoutPanel _list;
    private readonly Label _emptyLabel;
    private readonly TextBox _noteBox;
    private readonly Label _footer;

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
        KeyPreview = true;
        TopMost = true;

        // Sizes come from preferred content size, never fixed pixels, so the popup
        // stays correct at 125%, 150% and 200% display scaling.
        AutoScaleMode = AutoScaleMode.Font;

        var header = new Label
        {
            Text = "Page someone",
            Font = Theme.TitleFont,
            ForeColor = Theme.Text,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, Scale(10)),
        };

        _list = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            BackColor = Theme.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        _list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _emptyLabel = new Label
        {
            Text = "No other desks online yet.\r\n\r\nMake sure WinPager is running there, "
                 + "and that both PCs are on the same network.",
            Font = Theme.SmallFont,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(Scale(6), Scale(10), Scale(6), Scale(10)),
            Visible = false,
        };

        // Scrolls rather than growing without limit once the office gets big.
        _listHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Theme.Background,
            Margin = Padding.Empty,
        };
        _listHost.Controls.Add(_list);

        _noteBox = new TextBox
        {
            PlaceholderText = "Optional note, e.g. call on line 2",
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.BodyFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, Scale(10), 0, Scale(8)),
        };

        _footer = new Label
        {
            Font = Theme.SmallFont,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = Padding.Empty,
        };

        _root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(Scale(14), Scale(12), Scale(14), Scale(12)),
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // header
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // peer list
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // note
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // footer

        _root.Controls.Add(header, 0, 0);
        _root.Controls.Add(_listHost, 0, 1);
        _root.Controls.Add(_noteBox, 0, 2);
        _root.Controls.Add(_footer, 0, 3);

        Controls.Add(_root);

        _service.PeersChanged += OnPeersChanged;
        KeyDown += OnKeyDown;
    }

    /// <summary>How long ago this popup was last hidden. Lets the tray ignore the
    /// click that closed it, instead of treating it as a request to reopen.</summary>
    public double MillisecondsSinceHidden =>
        (DateTime.UtcNow - _hiddenAtUtc).TotalMilliseconds;

    private int Scale(int logicalPixels) => LogicalToDeviceUnits(logicalPixels);

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible) _hiddenAtUtc = DateTime.UtcNow;
        base.OnVisibleChanged(e);
    }

    /// <summary>Show the popup anchored to the notification area on the active screen.</summary>
    public void ShowNear()
    {
        _footer.Text = $"You are \"{_config.DisplayName}\"";
        _noteBox.Clear();
        Rebuild(_service.Peers);

        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            area.Right - Width - Scale(12),
            area.Bottom - Height - Scale(12));

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
        SuspendLayout();
        _list.SuspendLayout();

        // Dispose before clearing: once the collection is emptied there is nothing
        // left to enumerate, and the old buttons would leak their handles.
        var stale = _list.Controls.OfType<Button>().ToList();
        _list.Controls.Clear();
        foreach (var button in stale)
            button.Dispose();
        _list.RowStyles.Clear();

        if (peers.Count == 0)
        {
            _list.Controls.Add(_emptyLabel, 0, 0);
            _emptyLabel.Visible = true;
        }
        else
        {
            _emptyLabel.Visible = false;
            var row = 0;
            foreach (var peer in peers)
            {
                _list.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _list.Controls.Add(CreatePeerButton(peer), 0, row++);
            }
        }

        _list.ResumeLayout(performLayout: true);

        // Width is fixed; height follows the content, capped so a big office scrolls.
        var width = Scale(300);
        var listHeight = Math.Min(_list.PreferredSize.Height, Scale(360));
        _listHost.Height = listHeight;

        var chrome = _root.Padding.Vertical
                   + _root.GetControlFromPosition(0, 0)!.Height + Scale(10)
                   + _noteBox.Height + _noteBox.Margin.Vertical
                   + _footer.Height;

        ClientSize = new Size(width, listHeight + chrome);

        ResumeLayout(performLayout: true);
    }

    private Button CreatePeerButton(Peer peer)
    {
        var button = new Button
        {
            Text = peer.Name,
            Dock = DockStyle.Fill,
            Height = Scale(44),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.ButtonFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(Scale(12), 0, Scale(8), 0),
            Margin = new Padding(0, 0, 0, Scale(6)),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // A one-pixel border keeps the popup distinct from whatever is behind it.
        using var pen = new Pen(Theme.Divider);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
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
