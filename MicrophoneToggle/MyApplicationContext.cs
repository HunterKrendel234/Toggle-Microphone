using System.Runtime.InteropServices;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System.Text.Json;


namespace MicrophoneToggle
{
    public class Config
    {
        public string Language { get; set; } = "ru";
        public string Hotkey { get; set; } = "F9";
        public string Modifier { get; set; } = "None";
    }

    public class MyApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ToolStripMenuItem hotkeyToggleMenuItem;
        private ToolStripMenuItem hotkeySettingsMenuItem;
        private ToolStripMenuItem toggleItem;
        private ToolStripMenuItem exitItem;
        private ToolStripMenuItem langMenuItem;
        private HotkeyWindow hotkeyWindow;
        private const int HOTKEY_ID = 9000;
        private Icon iconOn;
        private Icon iconOff;

        private Config config;
        private readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data/config.json");
        private Dictionary<string, Dictionary<string, string>> translations;

        private readonly Dictionary<string, string> fallbackErrors = new Dictionary<string, string>
        {
            {"err_config_load", "Error loading config.json: {0}"},
            {"err_config_save", "Error saving config.json: {0}"},
            {"err_lang_load", "Error loading lang.json: {0}"},
            {"err_icons_load", "Error loading icons: {0}"},
            {"err_mic_status", "Error getting microphone status: {0}"},
            {"err_toggle_mic", "Error toggling microphone: {0}"},
            {"err_hotkey_register", "Failed to register hotkey: {0}"},
            {"err_title", "Error"}
        };

        private string currentLanguage = "ru";

        public MyApplicationContext()
        {
            LoadConfig();
            LoadTranslations();
            currentLanguage = config.Language;
            LoadIcons();
            InitializeTrayIcon();
            UpdateMicrophoneStatus();
            RegisterHotKey();
            AddToStartup();
        }

        #region Config
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<Config>(json);
                }
                else
                {
                    config = new Config();
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(T("err_config_load"), ex.Message), T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                config = new Config();
            }
        }

        private void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(T("err_config_save"), ex.Message), T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Lang
        private void LoadTranslations()
        {
            try
            {
                string langPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data/lang.json");
                string json = File.ReadAllText(langPath);
                translations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(T("err_lang_load"), ex.Message), T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                translations = new Dictionary<string, Dictionary<string, string>>();
            }
        }

        private string T(string key)
        {
            if (translations != null &&
                translations.ContainsKey(currentLanguage) &&
                translations[currentLanguage].ContainsKey(key))
                return translations[currentLanguage][key];
            if (fallbackErrors.ContainsKey(key))
                return fallbackErrors[key];
            return key;
        }
        #endregion

        #region Icon and UI
        private void LoadIcons()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                iconOn = new Icon(Path.Combine(baseDir, "style", "on.ico"));
                iconOff = new Icon(Path.Combine(baseDir, "style", "off.ico"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(T("err_icons_load"), ex.Message), T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                iconOn = SystemIcons.Application;
                iconOff = SystemIcons.Application;
            }
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = iconOn,
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();

            hotkeyToggleMenuItem = new ToolStripMenuItem(GetHotkeyDisplayText())
            {
                CheckOnClick = true,
                Checked = true
            };
            hotkeyToggleMenuItem.CheckedChanged += (s, e) =>
            {
                if (hotkeyToggleMenuItem.Checked)
                    RegisterHotKey();
                else
                    UnregisterHotKey();
            };

            hotkeySettingsMenuItem = new ToolStripMenuItem(T("hotkey_settings"), null, (s, e) => ShowHotkeySettings());

            toggleItem = new ToolStripMenuItem(T("toggle_mic"), null, (s, e) => ToggleMicrophone());
            exitItem = new ToolStripMenuItem(T("exit"), null, (s, e) => ExitApplication());


            langMenuItem = new ToolStripMenuItem(T("language"));
            var ruItem = new ToolStripMenuItem("RU");
            var enItem = new ToolStripMenuItem("EN");
            ruItem.Click += (s, e) => SwitchLanguage("ru");
            enItem.Click += (s, e) => SwitchLanguage("en");
            langMenuItem.DropDownItems.Add(ruItem);
            langMenuItem.DropDownItems.Add(enItem);


            contextMenu.Items.Add(hotkeyToggleMenuItem);
            contextMenu.Items.Add(hotkeySettingsMenuItem);
            contextMenu.Items.Add(toggleItem);
            contextMenu.Items.Add(langMenuItem);
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ToggleMicrophone();
            };
        }


        private void UpdateContextMenuText()
        {
            hotkeyToggleMenuItem.Text = GetHotkeyDisplayText();
            hotkeySettingsMenuItem.Text = T("hotkey_settings");
            toggleItem.Text = T("toggle_mic");
            exitItem.Text = T("exit");
            langMenuItem.Text = T("language");


            using var enumerator = new MMDeviceEnumerator();
            var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            bool isMuted = mic.AudioEndpointVolume.Mute;
            trayIcon.Text = isMuted ? T("mic_off") : T("mic_on");
        }

        private string GetHotkeyDisplayText()
        {
            string mod = config.Modifier != "None" ? config.Modifier + "+" : "";
            return string.Format(T("hotkey_active"), mod + config.Hotkey);
        }
        #endregion

        #region Hotkeys
        private void UpdateMicrophoneStatus()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                bool isMuted = mic.AudioEndpointVolume.Mute;
                trayIcon.Text = isMuted ? T("mic_off") : T("mic_on");
                trayIcon.Icon = isMuted ? iconOff : iconOn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(T("err_mic_status"), ex.Message), T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleMicrophone()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                mic.AudioEndpointVolume.Mute = !mic.AudioEndpointVolume.Mute;
                bool isMuted = mic.AudioEndpointVolume.Mute;
                trayIcon.Text = isMuted ? T("mic_off") : T("mic_on");
                trayIcon.Icon = isMuted ? iconOff : iconOn;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(T("err_toggle_mic"), ex.Message), T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RegisterHotKey()
        {
            if (hotkeyWindow != null)
                return;

            hotkeyWindow = new HotkeyWindow();
            Keys key;
            try
            {
                key = (Keys)Enum.Parse(typeof(Keys), config.Hotkey, true);
            }
            catch
            {
                key = Keys.F9;
            }
            uint modifier = ConvertModifier(config.Modifier);

            if (!hotkeyWindow.RegisterHotKey(HOTKEY_ID, modifier, key))
            {
                MessageBox.Show(string.Format(T("err_hotkey_register"), config.Hotkey), T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                hotkeyWindow.Dispose();
                hotkeyWindow = null;
            }
            else
            {
                hotkeyWindow.HotkeyPressed += ToggleMicrophone;
            }

            hotkeyToggleMenuItem.Text = GetHotkeyDisplayText();
        }

        private uint ConvertModifier(string mod)
        {
            switch (mod.ToLower())
            {
                case "shift":
                    return 4;
                case "alt":
                    return 1;
                case "control":
                    return 2;
                case "win":
                    return 8;
                default:
                    return 0;
            }
        }

        private void UnregisterHotKey()
        {
            if (hotkeyWindow != null)
            {
                hotkeyWindow.Dispose();
                hotkeyWindow = null;
            }
        }
        #endregion

        private void ExitApplication()
        {
            trayIcon.Visible = false;
            UnregisterHotKey();
            Application.Exit();
        }

        private void AddToStartup()
        {
            string appName = "MicrophoneToggleApp";
            string appPath = Application.ExecutablePath;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key.GetValue(appName) == null)
                {
                    key.SetValue(appName, appPath);
                    MessageBox.Show(T("added_to_startup"));
                }
            }
        }

        private void SwitchLanguage(string lang)
        {
            currentLanguage = lang;
            config.Language = lang;
            SaveConfig();
            UpdateContextMenuText();
        }

        private void ShowHotkeySettings()
        {
            using (var form = new HotkeySettingsForm(config.Hotkey, config.Modifier, T, currentLanguage))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    config.Hotkey = form.SelectedKey;
                    config.Modifier = form.SelectedModifier;
                    SaveConfig();
                    UnregisterHotKey();
                    RegisterHotKey();
                    UpdateContextMenuText();
                }
            }
        }
    }

    public delegate string Translator(string key);

    public class HotkeySettingsForm : Form
    {
        private ComboBox cbKey;
        private ComboBox cbModifier;
        private Button btnOK;
        private Button btnCancel;

        public string SelectedKey { get; private set; }
        public string SelectedModifier { get; private set; }

        private Translator T; 


        public HotkeySettingsForm(string currentKey, string currentModifier, Translator translator, string currentLanguage)
        {
            T = translator;


            this.Width = currentLanguage == "ru" ? 350 : 300;
            this.Height = 150;
            this.Text = T("hotkey_settings_form_title");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblKey = new Label() { Text = T("key_label"), Left = 10, Top = 10, Width = 100 };
            cbKey = new ComboBox() { Left = 110, Top = 10, Width = this.Width - 140, DropDownStyle = ComboBoxStyle.DropDownList };

            for (int i = 1; i <= 12; i++)
            {
                cbKey.Items.Add("F" + i);
            }
            cbKey.SelectedItem = currentKey;

            Label lblModifier = new Label() { Text = T("modifier_label"), Left = 10, Top = 40, Width = 100 };
            cbModifier = new ComboBox() { Left = 110, Top = 40, Width = this.Width - 140, DropDownStyle = ComboBoxStyle.DropDownList };
            cbModifier.Items.AddRange(new string[] { T("modifier_none"), T("modifier_shift"), T("modifier_alt"), T("modifier_control"), T("modifier_win") });
            cbModifier.SelectedItem = currentModifier;

            btnOK = new Button() { Text = T("ok"), Left = 110, Width = 75, Top = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button() { Text = T("cancel"), Left = 190, Width = 75, Top = 80, DialogResult = DialogResult.Cancel };

            btnOK.Click += (s, e) =>
            {
                SelectedKey = cbKey.SelectedItem.ToString();
                SelectedModifier = cbModifier.SelectedItem.ToString();
                this.Close();
            };
            btnCancel.Click += (s, e) => { this.Close(); };

            this.Controls.Add(lblKey);
            this.Controls.Add(cbKey);
            this.Controls.Add(lblModifier);
            this.Controls.Add(cbModifier);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }


    public class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public event Action HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        public bool RegisterHotKey(int id, uint modifiers, Keys key)
        {
            return RegisterHotKey(this.Handle, id, modifiers, (uint)key);
        }

        public void UnregisterHotKey(int id)
        {
            UnregisterHotKey(this.Handle, id);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                HotkeyPressed?.Invoke();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
