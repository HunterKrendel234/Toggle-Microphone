namespace MicrophoneToggle;

public class HotkeySettingsForm : Form
{
    private ComboBox cbKey = null!;
    private ComboBox cbModifier = null!;
    private Button btnOK = null!;
    private Button btnCancel = null!;

    public string SelectedKey { get; private set; } = null!;
    public string SelectedModifier { get; private set; } = null!;

    private readonly Translator T;
    private readonly bool darkMode;

    public HotkeySettingsForm(string currentKey, string currentModifier, Translator translator, string currentLanguage, bool dark)
    {
        T = translator;
        darkMode = dark;

        this.Width = currentLanguage == "ru" ? 360 : 320;
        this.Height = 180;
        this.Text = T("hotkey_settings_form_title");
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        var lblKey = new Label { Text = T("key_label"), Left = 12, Top = 14, Width = 100 };
        cbKey = new ComboBox { Left = 120, Top = 12, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };

        for (int i = 1; i <= 12; i++)
            cbKey.Items.Add("F" + i);
        cbKey.Items.Add("Space");
        cbKey.SelectedItem = currentKey;

        var lblModifier = new Label { Text = T("modifier_label"), Left = 12, Top = 46, Width = 100 };
        cbModifier = new ComboBox { Left = 120, Top = 44, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        cbModifier.Items.AddRange(new[] { "None", "Shift", "Alt", "Control", "Win" });
        cbModifier.SelectedItem = currentModifier == "None" ? "None" : currentModifier;

        var infoLabel = new Label
        {
            Text = currentLanguage == "ru"
                ? "Сочетание будет работать в играх"
                : "Hotkey will work in games",
            Left = 12, Top = 76, Width = 330, Height = 20,
            Font = new Font("Segoe UI", 8f, FontStyle.Italic),
        };

        btnOK = new Button { Text = T("ok"), Left = 120, Width = 80, Top = 106, Height = 26, DialogResult = DialogResult.OK };
        btnCancel = new Button { Text = T("cancel"), Left = 208, Width = 80, Top = 106, Height = 26, DialogResult = DialogResult.Cancel };

        btnOK.Click += (_, _) =>
        {
            SelectedKey = cbKey.SelectedItem?.ToString() ?? "F9";
            SelectedModifier = cbModifier.SelectedItem?.ToString() ?? "None";
            this.Close();
        };

        this.Controls.AddRange(new Control[] { lblKey, cbKey, lblModifier, cbModifier, infoLabel, btnOK, btnCancel });
        this.AcceptButton = btnOK;
        this.CancelButton = btnCancel;

        if (darkMode)
            DarkThemeHelper.ApplyToForm(this, true);
    }
}

public delegate string Translator(string key);
