namespace MicrophoneToggle;

internal sealed class DarkContextMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkContextMenuRenderer() : base(new DarkColorTable()) { }
}

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => Color.FromArgb(70, 70, 70);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(70, 70, 70);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(70, 70, 70);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(50, 50, 50);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(50, 50, 50);
    public override Color MenuItemBorder => Color.FromArgb(90, 90, 90);
    public override Color ToolStripDropDownBackground => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientBegin => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientEnd => Color.FromArgb(40, 40, 40);
    public override Color SeparatorDark => Color.FromArgb(80, 80, 80);
    public override Color SeparatorLight => Color.FromArgb(60, 60, 60);
    public override Color CheckBackground => Color.FromArgb(50, 50, 50);
    public override Color CheckPressedBackground => Color.FromArgb(60, 60, 60);
    public override Color CheckSelectedBackground => Color.FromArgb(60, 60, 60);
    public override Color ButtonSelectedBorder => Color.FromArgb(90, 90, 90);
}

internal static class DarkThemeHelper
{
    public static void ApplyToForm(Form form, bool dark)
    {
        var handle = form.Handle;

        int useDark = dark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

        if (dark)
        {
            int round = NativeMethods.DWM_WCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        }

        if (dark)
        {
            form.BackColor = Color.FromArgb(32, 32, 32);
            form.ForeColor = Color.White;
        }
        else
        {
            form.BackColor = SystemColors.Window;
            form.ForeColor = SystemColors.ControlText;
        }

        StyleControls(form.Controls, dark);
    }

    private static void StyleControls(Control.ControlCollection controls, bool dark)
    {
        foreach (Control ctrl in controls)
        {
            if (dark)
            {
                switch (ctrl)
                {
                    case Label lbl:
                        lbl.ForeColor = Color.White;
                        lbl.BackColor = Color.Transparent;
                        break;
                    case Button btn:
                        btn.BackColor = Color.FromArgb(60, 60, 60);
                        btn.ForeColor = Color.White;
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
                        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
                        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 50);
                        break;
                    case ComboBox cb:
                        cb.BackColor = Color.FromArgb(45, 45, 45);
                        cb.ForeColor = Color.White;
                        cb.FlatStyle = FlatStyle.Flat;
                        break;
                    case CheckBox chk:
                        chk.ForeColor = Color.White;
                        break;
                    case Panel panel:
                        panel.BackColor = Color.FromArgb(32, 32, 32);
                        break;
                }
            }
            else
            {
                switch (ctrl)
                {
                    case Button btn:
                        btn.UseVisualStyleBackColor = true;
                        btn.FlatStyle = FlatStyle.Standard;
                        break;
                    case ComboBox cb:
                        cb.ResetBackColor();
                        cb.ResetForeColor();
                        break;
                }
            }

            if (ctrl.HasChildren)
                StyleControls(ctrl.Controls, dark);
        }
    }

    public static void ApplyToContextMenu(ContextMenuStrip menu, bool dark)
    {
        if (dark)
        {
            menu.Renderer = new DarkContextMenuRenderer();
            menu.ForeColor = Color.White;
            menu.BackColor = Color.FromArgb(40, 40, 40);

            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = Color.White;
                if (item is ToolStripDropDownItem dropDown)
                {
                    foreach (ToolStripItem subItem in dropDown.DropDownItems)
                        subItem.ForeColor = Color.White;
                }
            }
        }
        else
        {
            menu.Renderer = new ToolStripProfessionalRenderer();
            menu.ForeColor = SystemColors.MenuText;
            menu.BackColor = SystemColors.Menu;

            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = SystemColors.MenuText;
                if (item is ToolStripDropDownItem dropDown)
                {
                    foreach (ToolStripItem subItem in dropDown.DropDownItems)
                        subItem.ForeColor = SystemColors.MenuText;
                }
            }
        }
    }
}
