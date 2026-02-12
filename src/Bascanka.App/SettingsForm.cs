namespace Bascanka.App;

/// <summary>
/// Settings dialog allowing the user to configure application options
/// such as Windows Explorer context menu integration.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly CheckBox _contextMenuCheckBox;

    public SettingsForm()
    {
        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.FromArgb(220, 220, 220);
        ClientSize = new Size(500, 200);

        // ── Explorer context menu ────────────────────────────────────
        _contextMenuCheckBox = new CheckBox
        {
            Text = Strings.SettingsExplorerContextMenu,
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(220, 220, 220),
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Location = new Point(24, 24),
            Checked = SettingsManager.IsExplorerContextMenuRegistered(),
        };

        var descLabel = new Label
        {
            Text = Strings.SettingsExplorerContextMenuDesc,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(150, 150, 150),
            AutoSize = true,
            MaximumSize = new Size(450, 0),
            Location = new Point(42, 52),
        };

        // ── Buttons ──────────────────────────────────────────────────
        var okButton = new Button
        {
            Text = Strings.ButtonOK,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9.5f),
            Size = new Size(90, 32),
            Location = new Point(ClientSize.Width - 200, ClientSize.Height - 46),
        };
        okButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        okButton.Click += OnOkClick;

        var cancelButton = new Button
        {
            Text = Strings.ButtonCancel,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9.5f),
            Size = new Size(90, 32),
            Location = new Point(ClientSize.Width - 100, ClientSize.Height - 46),
        };
        cancelButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(_contextMenuCheckBox);
        Controls.Add(descLabel);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        bool wantRegistered = _contextMenuCheckBox.Checked;
        bool isRegistered = SettingsManager.IsExplorerContextMenuRegistered();

        if (wantRegistered && !isRegistered)
            SettingsManager.RegisterExplorerContextMenu();
        else if (!wantRegistered && isRegistered)
            SettingsManager.UnregisterExplorerContextMenu();
    }
}
