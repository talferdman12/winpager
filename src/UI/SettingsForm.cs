namespace WinPager.UI;

/// <summary>Lets the user name this desk and pick their alert preferences.</summary>
public sealed class SettingsForm : Form
{
    private readonly Config _config;
    private readonly TableLayoutPanel _root;
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
        ShowInTaskbar = true;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.BodyFont;

        // Everything below is laid out by preferred size rather than fixed pixels,
        // so it stays readable at 125%, 150% and 200% display scaling.
        AutoScaleMode = AutoScaleMode.Font;

        _root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            BackColor = Theme.Background,
            Padding = new Padding(Scale(20), Scale(18), Scale(20), Scale(18)),
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _nameBox = new TextBox
        {
            Text = config.DisplayName,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.TitleFont,
            MaxLength = 40,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, Scale(6), 0, Scale(4)),
        };

        _soundBox = MakeCheckBox("Play a sound when I'm paged", config.SoundOnPage);
        _flashBox = MakeCheckBox("Show a large on-screen alert, not just the notification", config.FlashWindowOnPage);
        _startupBox = MakeCheckBox("Start automatically when Windows starts", config.StartWithWindows);

        var save = MakeButton("Save", DialogResult.OK, Theme.Accent, Color.White);
        var cancel = MakeButton("Cancel", DialogResult.Cancel, Theme.Surface, Theme.Text);

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, Scale(16), 0, 0),
            BackColor = Theme.Background,
        };
        buttonRow.Controls.Add(cancel);
        buttonRow.Controls.Add(save);

        _root.Controls.Add(MakeLabel("This desk is called:", Theme.BodyFont, Theme.Text));
        _root.Controls.Add(_nameBox);
        _root.Controls.Add(MakeLabel(
            "Everyone else sees this name on their page button.", Theme.SmallFont, Theme.TextMuted));
        _root.Controls.Add(MakeSpacer());
        _root.Controls.Add(_soundBox);
        _root.Controls.Add(_flashBox);
        _root.Controls.Add(_startupBox);
        _root.Controls.Add(buttonRow);

        Controls.Add(_root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    /// <summary>True when the display name changed, so the caller can re-announce.</summary>
    public bool NameChanged { get; private set; }

    private int Scale(int logicalPixels) => LogicalToDeviceUnits(logicalPixels);

    private Label MakeLabel(string text, Font font, Color color) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, Scale(2)),
        MaximumSize = new Size(Scale(400), 0),
    };

    private Label MakeSpacer() => new()
    {
        AutoSize = false,
        Height = Scale(10),
        Width = 1,
        Margin = Padding.Empty,
    };

    private CheckBox MakeCheckBox(string text, bool isChecked) => new()
    {
        Text = text,
        Checked = isChecked,
        ForeColor = Theme.Text,
        BackColor = Theme.Background,
        FlatStyle = FlatStyle.Flat,
        AutoSize = true,
        Margin = new Padding(0, Scale(3), 0, Scale(3)),
        MaximumSize = new Size(Scale(400), 0),
    };

    private Button MakeButton(string text, DialogResult result, Color back, Color fore)
    {
        var button = new Button
        {
            Text = text,
            DialogResult = result,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(Scale(90), Scale(32)),
            Padding = new Padding(Scale(10), Scale(4), Scale(10), Scale(4)),
            Margin = new Padding(Scale(8), 0, 0, 0),
            Cursor = Cursors.Hand,
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        DarkMode.Apply(this);

        // Size the dialog to whatever the content actually needs at this DPI.
        _root.Width = Scale(420);
        ClientSize = new Size(_root.Width, _root.PreferredSize.Height);
        CenterToScreen();
    }

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
