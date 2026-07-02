using System.Runtime.InteropServices;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System.Text.Json;

namespace MicrophoneToggle;

public class Config
{
    public string Language { get; set; } = "ru";
    public string Hotkey { get; set; } = "F9";
    public string Modifier { get; set; } = "None";
    public bool OverlayEnabled { get; set; } = true;
    public string OverlayPosition { get; set; } = "BottomRight";
    public string OverlaySize { get; set; } = "Small";
    public int OverlayAlpha { get; set; } = 200;
    public bool OverlayFontAlphaEnabled { get; set; } = false;
    public int OverlayFontAlpha { get; set; } = 255;
}

public class MyApplicationContext : ApplicationContext
{
    private NotifyIcon trayIcon = null!;
    private ToolStripMenuItem hotkeyToggleItem = null!;
    private ToolStripMenuItem hotkeySettingsItem = null!;
    private ToolStripMenuItem toggleItem = null!;
    private ToolStripMenuItem exitItem = null!;
    private ToolStripMenuItem overlaySettingsItem = null!;
    private ToolStripMenuItem langItem = null!;
    private OverlayForm? overlayForm;

    private Config config = null!;
    private readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "config.json");
    private Dictionary<string, Dictionary<string, string>>? translations;
    private string currentLanguage = "ru";
    private ContextMenuStrip contextMenu = null!;

    private Icon iconOn = null!;
    private Icon iconOff = null!;

    private MMDeviceEnumerator? deviceEnumerator;
    private MMDevice? defaultMic;

    private NativeMethods.LowLevelKeyboardProc hookProc = null!;
    private IntPtr hookId = IntPtr.Zero;
    private bool[] keyStates = new bool[256];
    private bool hookActive;

    private static readonly Dictionary<string, string> fallbackErrors = new()
    {
        ["err_config_load"] = "Error loading config.json: {0}",
        ["err_config_save"] = "Error saving config.json: {0}",
        ["err_lang_load"] = "Error loading lang.json: {0}",
        ["err_icons_load"] = "Error loading icons: {0}",
        ["err_mic_status"] = "Error getting microphone status: {0}",
        ["err_toggle_mic"] = "Error toggling microphone: {0}",
        ["err_hotkey"] = "Hotkey error: {0}",
        ["err_title"] = "Error",
    };

    public MyApplicationContext()
    {
        LoadConfig();
        LoadTranslations();
        currentLanguage = config.Language;
        InitAudio();
        LoadIcons();
        InitializeTrayIcon();
        UpdateMicStatus();
        InitLowLevelHook();
        AddToStartup();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.VisualStyle)
        {
            if (contextMenu != null && contextMenu.InvokeRequired)
                contextMenu.BeginInvoke(ApplyTheme);
            else
                ApplyTheme();
        }
    }

    private bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int val)
                return val == 0;
        }
        catch { }
        return false;
    }

    private void ApplyTheme()
    {
        bool dark = IsWindowsDarkMode();
        DarkThemeHelper.ApplyToContextMenu(contextMenu, dark);
        if (trayIcon != null)
            trayIcon.ContextMenuStrip = contextMenu;
    }

    private void InitAudio()
    {
        try
        {
            deviceEnumerator = new MMDeviceEnumerator();
            defaultMic = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        }
        catch (Exception ex)
        {
            ShowError(string.Format(T("err_mic_status"), ex.Message));
        }
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<Config>(json)!;
            }
            else
            {
                config = new Config();
                SaveConfig();
            }
        }
        catch
        {
            config = new Config();
        }
    }

    private void SaveConfig()
    {
        try
        {
            string? dir = Path.GetDirectoryName(configPath);
            if (dir != null) Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            ShowError(string.Format(T("err_config_save"), ex.Message));
        }
    }

    private void LoadTranslations()
    {
        try
        {
            string langPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "lang.json");
            string json = File.ReadAllText(langPath);
            translations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
        }
        catch
        {
            translations = null;
        }
    }

    private string T(string key)
    {
        if (translations != null &&
            translations.TryGetValue(currentLanguage, out var lang) &&
            lang.TryGetValue(key, out var val))
            return val;

        return fallbackErrors.GetValueOrDefault(key, key);
    }

    private void LoadIcons()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            iconOn = new Icon(Path.Combine(baseDir, "style", "on.ico"));
            iconOff = new Icon(Path.Combine(baseDir, "style", "off.ico"));
        }
        catch
        {
            iconOn = SystemIcons.Application;
            iconOff = SystemIcons.Application;
        }
    }

    private void InitializeTrayIcon()
    {
        trayIcon = new NotifyIcon
        {
            Icon = iconOn,
            Visible = true,
        };

        contextMenu = new ContextMenuStrip();

        hotkeyToggleItem = new ToolStripMenuItem(GetHotkeyDisplayText())
        {
            CheckOnClick = true,
            Checked = hookActive,
        };
        hotkeyToggleItem.CheckedChanged += (_, _) =>
        {
            if (hotkeyToggleItem.Checked)
                InitLowLevelHook();
            else
                RemoveLowLevelHook();
        };

        hotkeySettingsItem = new ToolStripMenuItem(T("hotkey_settings"), null, (_, _) => ShowHotkeySettings());
        toggleItem = new ToolStripMenuItem(T("toggle_mic"), null, (_, _) => ToggleMicrophone());

        overlaySettingsItem = new ToolStripMenuItem(T("overlay_settings"), null, (_, _) => ShowOverlaySettings());

        langItem = new ToolStripMenuItem(T("language"));
        var ruItem = new ToolStripMenuItem("RU", null, (_, _) => SwitchLanguage("ru"));
        var enItem = new ToolStripMenuItem("EN", null, (_, _) => SwitchLanguage("en"));
        langItem.DropDownItems.Add(ruItem);
        langItem.DropDownItems.Add(enItem);

        exitItem = new ToolStripMenuItem(T("exit"), null, (_, _) => ExitApplication());

        contextMenu.Items.Add(hotkeyToggleItem);
        contextMenu.Items.Add(hotkeySettingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(toggleItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(overlaySettingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(langItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        ApplyTheme();

        trayIcon.ContextMenuStrip = contextMenu;
        trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ToggleMicrophone();
        };
    }

    private void UpdateMenuText()
    {
        hotkeyToggleItem.Text = GetHotkeyDisplayText();
        hotkeySettingsItem.Text = T("hotkey_settings");
        toggleItem.Text = T("toggle_mic");
        overlaySettingsItem.Text = T("overlay_settings");
        exitItem.Text = T("exit");
        langItem.Text = T("language");
    }

    private string GetHotkeyDisplayText()
    {
        string mod = config.Modifier != "None" ? config.Modifier + "+" : "";
        string hotkey = mod + config.Hotkey;
        return hookActive
            ? string.Format(T("hotkey_active"), hotkey)
            : T("hotkey_inactive");
    }

    private void UpdateMicStatus()
    {
        try
        {
            if (defaultMic == null || defaultMic.AudioEndpointVolume == null)
            {
                RefreshAudioDevice();
                if (defaultMic == null) return;
            }

            bool isMuted = defaultMic.AudioEndpointVolume!.Mute;
            trayIcon.Text = isMuted ? T("mic_off") : T("mic_on");
            trayIcon.Icon = isMuted ? iconOff : iconOn;
        }
        catch
        {
            RefreshAudioDevice();
        }
    }

    private void RefreshAudioDevice()
    {
        try
        {
            deviceEnumerator?.Dispose();
            deviceEnumerator = new MMDeviceEnumerator();
            defaultMic = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        }
        catch
        {
            defaultMic = null;
        }
    }

    private OverlayForm GetOverlay()
    {
        if (overlayForm == null || overlayForm.IsDisposed)
            overlayForm = new OverlayForm();
        return overlayForm;
    }

    private void ShowOverlayNotification(bool isMuted)
    {
        if (!config.OverlayEnabled) return;
        try
        {
            var overlay = GetOverlay();
            overlay.ShowNotification(isMuted, config.OverlayPosition, config.OverlaySize,
                (byte)config.OverlayAlpha, (byte)config.OverlayFontAlpha,
                config.OverlayFontAlphaEnabled);
        }
        catch (Exception ex)
        {
            trayIcon.ShowBalloonTip(5000, "Overlay Error", ex.ToString(), ToolTipIcon.Error);
        }
    }

    private void ToggleMicrophone()
    {
        bool isMuted;
        try
        {
            if (defaultMic == null || defaultMic.AudioEndpointVolume == null)
            {
                RefreshAudioDevice();
                if (defaultMic == null) return;
            }

            var vol = defaultMic.AudioEndpointVolume!;
            vol.Mute = !vol.Mute;
            isMuted = vol.Mute;
            trayIcon.Text = isMuted ? T("mic_off") : T("mic_on");
            trayIcon.Icon = isMuted ? iconOff : iconOn;
        }
        catch
        {
            RefreshAudioDevice();
            return;
        }
        ShowOverlayNotification(isMuted);
    }

    private void InitLowLevelHook()
    {
        if (hookActive) return;

        hookProc = LowLevelHookCallback;
        hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            hookProc,
            NativeMethods.GetModuleHandle(null),
            0);

        hookActive = hookId != IntPtr.Zero;

        if (!hookActive)
        {
            ShowError(string.Format(T("err_hotkey"), Marshal.GetLastWin32Error()));
        }

        if (hotkeyToggleItem != null)
        {
            hotkeyToggleItem.Checked = hookActive;
            hotkeyToggleItem.Text = GetHotkeyDisplayText();
        }
    }

    private void RemoveLowLevelHook()
    {
        if (!hookActive) return;

        NativeMethods.UnhookWindowsHookEx(hookId);
        hookId = IntPtr.Zero;
        hookActive = false;
        Array.Clear(keyStates, 0, keyStates.Length);

        if (hotkeyToggleItem != null)
        {
            hotkeyToggleItem.Checked = false;
            hotkeyToggleItem.Text = GetHotkeyDisplayText();
        }
    }

    private IntPtr LowLevelHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool keyDown = wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN;
            bool keyUp = wParam == (IntPtr)NativeMethods.WM_KEYUP || wParam == (IntPtr)NativeMethods.WM_SYSKEYUP;

            if (vkCode >= 0 && vkCode < 256)
            {
                if (keyDown) keyStates[vkCode] = true;
                else if (keyUp) keyStates[vkCode] = false;
            }

            if (keyDown && IsHotkeyMatch(vkCode))
            {
                ToggleMicrophone();
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    private bool IsHotkeyMatch(int vkCode)
    {
        Keys targetKey;
        try { targetKey = (Keys)Enum.Parse(typeof(Keys), config.Hotkey, true); }
        catch { targetKey = Keys.F9; }

        if (vkCode != (int)targetKey) return false;

        bool needCtrl = config.Modifier.Equals("Control", StringComparison.OrdinalIgnoreCase);
        bool needAlt = config.Modifier.Equals("Alt", StringComparison.OrdinalIgnoreCase);
        bool needShift = config.Modifier.Equals("Shift", StringComparison.OrdinalIgnoreCase);
        bool needWin = config.Modifier.Equals("Win", StringComparison.OrdinalIgnoreCase);

        if (!needCtrl && !needAlt && !needShift && !needWin) return true;

        bool ctrl = keyStates[(int)Keys.ControlKey] || keyStates[(int)Keys.LControlKey] || keyStates[(int)Keys.RControlKey];
        bool alt = keyStates[(int)Keys.Menu] || keyStates[(int)Keys.LMenu] || keyStates[(int)Keys.RMenu];
        bool shift = keyStates[(int)Keys.ShiftKey] || keyStates[(int)Keys.LShiftKey] || keyStates[(int)Keys.RShiftKey];
        bool win = keyStates[(int)Keys.LWin] || keyStates[(int)Keys.RWin];

        if (needCtrl && !ctrl) return false;
        if (needAlt && !alt) return false;
        if (needShift && !shift) return false;
        if (needWin && !win) return false;

        return true;
    }

    private void ShowHotkeySettings()
    {
        using (var form = new HotkeySettingsForm(config.Hotkey, config.Modifier, T, currentLanguage, IsWindowsDarkMode()))
        {
            if (form.ShowDialog() == DialogResult.OK)
            {
                config.Hotkey = form.SelectedKey;
                config.Modifier = form.SelectedModifier;
                SaveConfig();
                RemoveLowLevelHook();
                InitLowLevelHook();
                UpdateMenuText();
            }
        }
    }

    private void ShowOverlaySettings()
    {
        using (var form = new OverlaySettingsForm(
            config.OverlayEnabled, config.OverlayPosition, config.OverlaySize, config.OverlayAlpha,
            config.OverlayFontAlphaEnabled, config.OverlayFontAlpha,
            T, currentLanguage, IsWindowsDarkMode()))
        {
            if (form.ShowDialog() == DialogResult.OK)
            {
                config.OverlayEnabled = form.OverlayEnabled;
                config.OverlayPosition = form.OverlayPosition;
                config.OverlaySize = form.OverlaySize;
                config.OverlayAlpha = form.OverlayAlpha;
                config.OverlayFontAlphaEnabled = form.OverlayFontAlphaEnabled;
                config.OverlayFontAlpha = form.OverlayFontAlpha;
                SaveConfig();
                if (config.OverlayEnabled)
                    ShowOverlayNotification(GetMicMuted());
            }
        }
    }

    private bool GetMicMuted()
    {
        try
        {
            if (defaultMic == null || defaultMic.AudioEndpointVolume == null)
                RefreshAudioDevice();
            return defaultMic?.AudioEndpointVolume?.Mute ?? false;
        }
        catch { return false; }
    }

    private void SwitchLanguage(string lang)
    {
        currentLanguage = lang;
        config.Language = lang;
        SaveConfig();
        UpdateMenuText();
        UpdateMicStatus();
    }

    private void AddToStartup()
    {
        try
        {
            string appName = "MicrophoneToggleApp";
            string appPath = Application.ExecutablePath;
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key?.GetValue(appName) == null)
            {
                key?.SetValue(appName, appPath);
            }
        }
        catch { }
    }

    private void ExitApplication()
    {
        RemoveLowLevelHook();
        if (overlayForm != null && !overlayForm.IsDisposed)
            overlayForm.Dispose();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        deviceEnumerator?.Dispose();
        Application.Exit();
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, T("err_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
