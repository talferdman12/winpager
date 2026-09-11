namespace WinPager.UI;

/// <summary>Lets the user name this desk and pick their alert preferences.</summary>
public sealed class SettingsForm : Form
{
    private readonly Config _config;
    private readonly TextBox _nameBox;
    private readonly CheckBox _soundBox;
    private readonly CheckBox _flashBox;
    private readonly CheckBox _startupBox;

    public SettingsForm(Config config)
    {
        _config = config;

        Text = "WinPager — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(400, 260);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.SmallFont;

        var nameLabel = new Label
        {
            Text = "This desk is called:",
            Location = new Point(20, 22),
            Size = new Size(360, 20),
            ForeColor = Theme.Text,
        };

        _nameBox = new TextBox
        {
            Text = config.DisplayName,
            Location = new Point(20, 46),
            Size = new Size(360, 26),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.TitleFont,
            MaxLength = 40,
        };

        var hint = new Label
        {
            Text = "Everyone else sees this name on their page button.",
            Location = new Point(20, 78),
            Size = new Size(360, 18),
            ForeColor = Theme.TextMuted,
        };

        _soundBox = new CheckBox
        {
            Text = "Play a sound when I'm paged",
            Checked = config.SoundOnPage,
            Location = new Point(20, 110),
            Size = new Size(360, 24),
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat,
        };

        _flashBox = new CheckBox
        {
            Text = "Show a large on-screen alert (not just the notification)",
            Checked = config.FlashWindowOnPage,
            Location = new Point(20, 138),
            Size = new Size(360, 24),
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat,
        };

        _startupBox = new CheckBox
        {
            Text = "Start automatically when Windows starts",
            Checked = config.StartWithWindows,
            Location = new Point(20, 166),
            Size = new Size(360, 24),
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat,
        };

        var save = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(212, 210),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Color.White,
        };
        save.FlatAppearance.BorderSize = 0;

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(300, 210),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
        };
        cancel.FlatAppearance.BorderSize = 0;

        Controls.AddRange([nameLabel, _nameBox, hint, _soundBox, _flashBox, _startupBox, save, cancel]);
        AcceptButton = save;
        CancelButton = cancel;
    }

    /// <summary>True when the display name changed, so the caller can re-announce.</summary>
    public bool NameChanged { get; private set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            var newName = _nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show(this, "Please give this desk a name.", "WinPager",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

            NameChanged = !string.Equals(newName, _config.DisplayName, StringComparison.Ordinal);

            _config.DisplayName = newName;
            _config.SoundOnPage = _soundBox.Checked;
            _config.FlashWindowOnPage = _flashBox.Checked;
            _config.StartWithWindows = _startupBox.Checked;
            _config.Save();

            Startup.Apply(_config.StartWithWindows);
        }

        base.OnFormClosing(e);
    }
}
