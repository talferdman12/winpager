using ChabadOfficePager.Net;

namespace ChabadOfficePager.UI;

/// <summary>
/// Owns the tray icon and wires the network service to the UI.
/// This is the app's real "main window" — there isn't a visible one.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly Config _config;
    private readonly PeerService _service;
    private readonly NotifyIcon _tray;
    private readonly PagerPopup _popup;
    private readonly ToolStripMenuItem _peersHeader;
    private readonly ToolStripMenuItem _quickPageRoot;

    public TrayContext(Config config)
    {
        _config = config;
        _service = new PeerService(config);
        _popup = new PagerPopup(_service, config);

        _peersHeader = new ToolStripMenuItem("Looking for other desks…") { Enabled = false };
        _quickPageRoot = new ToolStripMenuItem("Page…");

        var menu = new ContextMenuStrip();
        menu.Items.Add(_peersHeader);
        menu.Items.Add(_quickPageRoot);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open pager", null, (_, _) => TogglePopup());
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon = Theme.IdleIcon,
            Text = $"Chabad Office Pager — {config.DisplayName}",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _tray.MouseClick += OnTrayClick;
        _tray.BalloonTipClicked += (_, _) => _tray.Icon = Theme.IdleIcon;

        _service.PeersChanged += OnPeersChanged;
        _service.PageReceived += OnPageReceived;
        _service.PageDelivered += OnPageDelivered;
        _service.PageFailed += OnPageFailed;
        _service.NetworkError += OnNetworkError;

        _service.Start();
        Startup.Apply(config.StartWithWindows);

        _tray.ShowBalloonTip(3000, "Chabad Office Pager",
            $"Running as \"{config.DisplayName}\". Click the tray icon to page someone.",
            ToolTipIcon.Info);
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            TogglePopup();
    }

    private void TogglePopup()
    {
        _tray.Icon = Theme.IdleIcon;

        if (_popup.Visible)
        {
            _popup.Hide();
            return;
        }

        // Clicking the tray icon while the popup is open makes Windows deactivate it
        // (which hides it) before this handler runs. Without this guard, the click
        // that should close the popup would immediately reopen it.
        if (_popup.MillisecondsSinceHidden < 300)
            return;

        _popup.ShowNear();
    }

    private void OnPeersChanged(IReadOnlyList<Peer> peers)
    {
        _peersHeader.Text = peers.Count switch
        {
            0 => "No other desks online",
            1 => "1 other desk online",
            _ => $"{peers.Count} other desks online",
        };

        // Rebuild the right-click shortcut list so paging never needs the fly-out.
        // Copy first: disposing an item removes it from the collection we'd be walking.
        foreach (var item in _quickPageRoot.DropDownItems.Cast<ToolStripItem>().ToArray())
            item.Dispose();
        _quickPageRoot.DropDownItems.Clear();

        if (peers.Count == 0)
        {
            _quickPageRoot.Enabled = false;
            return;
        }

        _quickPageRoot.Enabled = true;
        foreach (var peer in peers)
        {
            var id = peer.Id;
            _quickPageRoot.DropDownItems.Add(peer.Name, null, (_, _) => _service.SendPage(id, null));
        }
    }

    private void OnPageReceived(PagerMessage page)
    {
        _tray.Icon = Theme.AlertIcon;

        var body = string.IsNullOrWhiteSpace(page.Text)
            ? $"{page.SenderName} is paging you."
            : $"{page.SenderName}: {page.Text}";

        // The Windows notification. On Windows 10/11 this surfaces as a normal toast
        // in the Action Center, so it persists if the user is away from the desk.
        _tray.ShowBalloonTip(10000, "📟 You're being paged", body, ToolTipIcon.Warning);

        if (_config.FlashWindowOnPage)
        {
            var alert = new AlertForm(page, _config.SoundOnPage);
            alert.Show();
        }
        else if (_config.SoundOnPage)
        {
            try { System.Media.SystemSounds.Exclamation.Play(); }
            catch (Exception ex) { Log.Write($"Sound failed: {ex.Message}"); }
        }
    }

    private void OnPageDelivered(string peerName) =>
        _tray.ShowBalloonTip(2500, "Page sent", $"{peerName} got your page.", ToolTipIcon.Info);

    private void OnPageFailed(string peerName) =>
        _tray.ShowBalloonTip(5000, "Page not delivered",
            $"{peerName} did not respond. Their PC may be asleep or off the network.",
            ToolTipIcon.Error);

    private void OnNetworkError(string message) =>
        _tray.ShowBalloonTip(8000, "Chabad Office Pager", message, ToolTipIcon.Error);

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config);
        if (form.ShowDialog() != DialogResult.OK) return;

        _tray.Text = $"Chabad Office Pager — {_config.DisplayName}";
        if (form.NameChanged)
            _service.AnnounceNow();
    }

    private void ExitApp()
    {
        _tray.Visible = false;
        _service.Dispose();
        _popup.Dispose();
        _tray.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _service.Dispose();
            _tray.Dispose();
        }

        base.Dispose(disposing);
    }
}
