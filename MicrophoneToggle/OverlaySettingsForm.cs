namespace MicrophoneToggle;

public class OverlaySettingsForm : Form
{
    private CheckBox chkEnabled = null!;
    private ComboBox cbPosition = null!;
    private ComboBox cbSize = null!;
    private TrackBar tbAlpha = null!;
    private Label lblAlphaValue = null!;
    private CheckBox chkFontAlpha = null!;
    private TrackBar tbFontAlpha = null!;
    private Label lblFontAlphaValue = null!;
    private Label lblFontAlpha = null!;
    private Button btnOK = null!;
    private Button btnCancel = null!;

    public bool OverlayEnabled { get; private set; }
    public string OverlayPosition { get; private set; } = null!;
    public string OverlaySize { get; private set; } = null!;
    public int OverlayAlpha { get; private set; }
    public bool OverlayFontAlphaEnabled { get; private set; }
    public int OverlayFontAlpha { get; private set; }

    private readonly Translator T;
    private readonly bool darkMode;

    public OverlaySettingsForm(bool enabled, string position, string size, int alpha,
        bool fontAlphaEnabled, int fontAlpha,
        Translator translator, string currentLanguage, bool dark)
    {
        T = translator;
        darkMode = dark;

        Width = 400;
        Height = 370;
        Text = T("overlay_settings_title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        chkEnabled = new CheckBox
        {
            Text = T("overlay_enable"),
            Left = 16, Top = 16, Width = 360, Height = 26,
            Checked = enabled,
            Font = new Font("Segoe UI", 10),
        };

        var lblPos = new Label { Text = T("overlay_position"), Left = 16, Top = 54, Width = 120, Height = 22 };
        cbPosition = new ComboBox
        {
            Left = 140, Top = 52, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList,
        };
        cbPosition.Items.AddRange(new[]
        {
            T("overlay_pos_bottomright"),
            T("overlay_pos_bottomleft"),
            T("overlay_pos_topright"),
            T("overlay_pos_topleft"),
            T("overlay_pos_centertop"),
            T("overlay_pos_centerbottom"),
        });
        cbPosition.SelectedIndex = position switch
        {
            "BottomLeft" => 1, "TopRight" => 2, "TopLeft" => 3,
            "CenterTop" => 4, "CenterBottom" => 5, _ => 0,
        };

        var lblSize = new Label { Text = T("overlay_size"), Left = 16, Top = 90, Width = 120, Height = 22 };
        cbSize = new ComboBox
        {
            Left = 140, Top = 88, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList,
        };
        cbSize.Items.AddRange(new[] { T("overlay_size_small"), T("overlay_size_normal") });
        cbSize.SelectedIndex = size == "Small" ? 0 : 1;

        var lblAlpha = new Label { Text = T("overlay_alpha_label"), Left = 16, Top = 126, Width = 120, Height = 22 };
        lblAlphaValue = new Label
        {
            Left = 290, Top = 126, Width = 60, Height = 22,
            Text = (int)Math.Round(alpha / 255.0 * 100) + "%",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9),
        };
        tbAlpha = new TrackBar
        {
            Left = 136, Top = 122, Width = 150, Height = 30,
            Minimum = 0, Maximum = 255, Value = alpha,
            TickFrequency = 32, SmallChange = 8, LargeChange = 32,
        };
        tbAlpha.ValueChanged += (_, _) =>
        {
            lblAlphaValue.Text = (int)Math.Round(tbAlpha.Value / 255.0 * 100) + "%";
        };

        chkFontAlpha = new CheckBox
        {
            Text = T("overlay_font_alpha"),
            Left = 16, Top = 164, Width = 360, Height = 26,
            Checked = fontAlphaEnabled,
            Font = new Font("Segoe UI", 10),
        };

        lblFontAlpha = new Label { Text = T("overlay_font_alpha_label"), Left = 16, Top = 196, Width = 120, Height = 22 };
        lblFontAlphaValue = new Label
        {
            Left = 290, Top = 196, Width = 60, Height = 22,
            Text = (int)Math.Round(fontAlpha / 255.0 * 100) + "%",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9),
        };
        tbFontAlpha = new TrackBar
        {
            Left = 136, Top = 192, Width = 150, Height = 30,
            Minimum = 0, Maximum = 255, Value = fontAlpha,
            TickFrequency = 32, SmallChange = 8, LargeChange = 32,
            Enabled = fontAlphaEnabled,
        };
        tbFontAlpha.ValueChanged += (_, _) =>
        {
            lblFontAlphaValue.Text = (int)Math.Round(tbFontAlpha.Value / 255.0 * 100) + "%";
        };
        chkFontAlpha.CheckedChanged += (_, _) =>
        {
            bool en = chkFontAlpha.Checked;
            tbFontAlpha.Enabled = en;
            lblFontAlpha.Enabled = en;
            lblFontAlphaValue.Enabled = en;
        };

        var previewLabel = new Label
        {
            Text = T("overlay_hint"),
            Left = 16, Top = 234, Width = 360, Height = 30,
            Font = new Font("Segoe UI", 9, FontStyle.Italic),
        };

        btnOK = new Button { Text = T("ok"), Left = 160, Width = 90, Height = 28, Top = 280 };
        btnCancel = new Button { Text = T("cancel"), Left = 262, Width = 90, Height = 28, Top = 280 };

        btnOK.Click += (_, _) =>
        {
            OverlayEnabled = chkEnabled.Checked;
            OverlayPosition = cbPosition.SelectedIndex switch
            {
                1 => "BottomLeft", 2 => "TopRight", 3 => "TopLeft",
                4 => "CenterTop", 5 => "CenterBottom", _ => "BottomRight",
            };
            OverlaySize = cbSize.SelectedIndex == 0 ? "Small" : "Normal";
            OverlayAlpha = tbAlpha.Value;
            OverlayFontAlphaEnabled = chkFontAlpha.Checked;
            OverlayFontAlpha = tbFontAlpha.Value;
            DialogResult = DialogResult.OK;
            Close();
        };
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange(new Control[] {
            chkEnabled, lblPos, cbPosition, lblSize, cbSize,
            lblAlpha, tbAlpha, lblAlphaValue,
            chkFontAlpha, lblFontAlpha, tbFontAlpha, lblFontAlphaValue,
            previewLabel, btnOK, btnCancel,
        });
        AcceptButton = btnOK;
        CancelButton = btnCancel;

        if (darkMode)
            DarkThemeHelper.ApplyToForm(this, true);
    }
}
