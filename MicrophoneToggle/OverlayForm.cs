using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MicrophoneToggle;

public class OverlayForm : Form
{
    private Font? currentFont;
    private byte bgAlpha = 200;
    private byte fontAlpha = 255;
    private bool fontAlphaSeparate;
    private string text = "🎤";
    private Color textColor = Color.FromArgb(52, 168, 83);
    private System.Windows.Forms.Timer hideTimer;
    private const int MARGIN = 32;
    private const int SIZE_SMALL = 24;
    private const int SIZE_NORMAL = 48;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(1, 1);

        hideTimer = new System.Windows.Forms.Timer { Interval = 1400 };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            Hide();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x80 | 0x08000000 | 0x20;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0084)
        {
            m.Result = (IntPtr)(-1);
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hideTimer?.Dispose();
            currentFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    private int DpiScale(int px) => (int)Math.Round(px * DeviceDpi / 96.0);

    private void RenderOverlay()
    {
        if (!IsHandleCreated) return;
        int w = Math.Max(Width, 1);
        int h = Math.Max(Height, 1);
        int rad = DpiScale(12);
        int tAlpha = fontAlphaSeparate ? fontAlpha : bgAlpha;
        int textMax = Math.Max(textColor.R, Math.Max(textColor.G, textColor.B));

        using var bgBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bgBmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var path = RoundedRect(0, 0, w, h, rad);
            using var brush = new SolidBrush(Color.FromArgb(255, 32, 32, 32));
            g.FillPath(brush, path);
        }

        var rect = new Rectangle(0, 0, w, h);
        var bgData = bgBmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride = bgData.Stride;
        int bytes = stride * h;
        var bgPixels = new byte[bytes];
        Marshal.Copy(bgData.Scan0, bgPixels, 0, bytes);
        bgBmp.UnlockBits(bgData);

        using var textBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(textBmp))
        {
            g.Clear(Color.Transparent);
            TextRenderer.DrawText(g, text, currentFont,
                new Rectangle(0, 0, w, h),
                Color.FromArgb(255, textColor.R, textColor.G, textColor.B),
                Color.Transparent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix);
        }

        var textData = textBmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var textPixels = new byte[bytes];
        Marshal.Copy(textData.Scan0, textPixels, 0, bytes);
        textBmp.UnlockBits(textData);

        var result = new byte[bytes];

        for (int i = 0; i < bytes; i += 4)
        {
            byte bgA = (byte)(bgAlpha * bgPixels[i + 3] / 255);
            int r = bgPixels[i + 2] * bgA / 255;
            int g = bgPixels[i + 1] * bgA / 255;
            int b = bgPixels[i]     * bgA / 255;
            int a = bgA;

            byte tb = textPixels[i];
            byte tg = textPixels[i + 1];
            byte tr = textPixels[i + 2];
            int textMaxCh = Math.Max(tr, Math.Max(tg, tb));
            if (textMaxCh > 0)
            {
                float cov = Math.Min(1f, textMaxCh / (float)textMax);
                int tA = (int)(cov * tAlpha);
                int tR = textColor.R * tA / 255;
                int tG = textColor.G * tA / 255;
                int tB = textColor.B * tA / 255;
                int invTA = 255 - tA;
                r = tR + r * invTA / 255;
                g = tG + g * invTA / 255;
                b = tB + b * invTA / 255;
                a = tA + a * invTA / 255;
            }

            result[i + 2] = (byte)r;
            result[i + 1] = (byte)g;
            result[i]     = (byte)b;
            result[i + 3] = (byte)a;
        }

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) return;

        var memDc = NativeMethods.CreateCompatibleDC(screenDc);
        if (memDc == IntPtr.Zero) { NativeMethods.ReleaseDC(IntPtr.Zero, screenDc); return; }

        IntPtr bmi = Marshal.AllocHGlobal(44);
        Marshal.WriteInt32(bmi, 0, 40);
        Marshal.WriteInt32(bmi, 4, w);
        Marshal.WriteInt32(bmi, 8, -h);
        Marshal.WriteInt16(bmi, 12, 1);
        Marshal.WriteInt16(bmi, 14, 32);
        Marshal.WriteInt32(bmi, 16, 0);

        IntPtr bits;
        var hDib = NativeMethods.CreateDIBSection(screenDc, bmi, 0, out bits, IntPtr.Zero, 0);
        Marshal.FreeHGlobal(bmi);

        if (hDib == IntPtr.Zero) { NativeMethods.DeleteDC(memDc); NativeMethods.ReleaseDC(IntPtr.Zero, screenDc); return; }

        Marshal.Copy(result, 0, bits, bytes);

        var oldObj = NativeMethods.SelectObject(memDc, hDib);

        var winPos = new NativeMethods.POINT { x = Left, y = Top };
        var winSize = new NativeMethods.SIZE { cx = w, cy = h };
        var srcPos = new NativeMethods.POINT { x = 0, y = 0 };
        var blend = new NativeMethods.BLENDFUNCTION
        {
            BlendOp = NativeMethods.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = NativeMethods.AC_SRC_ALPHA,
        };

        NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref winPos, ref winSize,
            memDc, ref srcPos, 0, ref blend, NativeMethods.ULW_ALPHA);

        NativeMethods.SelectObject(memDc, oldObj);
        NativeMethods.DeleteObject(hDib);
        NativeMethods.DeleteDC(memDc);
        NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
    }

    private static GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
    {
        var path = new GraphicsPath();
        int d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void ShowNotification(bool isMuted, string position, string sizeKey,
        byte backgroundAlpha, byte textAlpha = 255, bool separateFontAlpha = false)
    {
        hideTimer.Stop();

        bgAlpha = backgroundAlpha;
        fontAlpha = textAlpha;
        fontAlphaSeparate = separateFontAlpha;
        text = "🎤";
        textColor = isMuted
            ? Color.FromArgb(234, 67, 53)
            : Color.FromArgb(52, 168, 83);

        int fontSize = sizeKey == "Small" ? DpiScale(14) : DpiScale(16);
        if (currentFont != null) currentFont.Dispose();
        currentFont = new Font("Segoe UI", fontSize, FontStyle.Bold);

        int s = DpiScale(sizeKey == "Small" ? SIZE_SMALL : SIZE_NORMAL);

        var screen = Screen.PrimaryScreen;
        if (screen is null) return;
        var wa = screen.WorkingArea;
        int margin = DpiScale(MARGIN);

        int x = position switch
        {
            "TopLeft"      => wa.Left + margin,
            "TopRight"     => wa.Right - s - margin,
            "BottomLeft"   => wa.Left + margin,
            "BottomRight"  => wa.Right - s - margin,
            "CenterTop"    => wa.Left + (wa.Width - s) / 2,
            "CenterBottom" => wa.Left + (wa.Width - s) / 2,
            _              => wa.Right - s - margin,
        };
        int y = position switch
        {
            "TopLeft"      => wa.Top + margin,
            "TopRight"     => wa.Top + margin,
            "BottomLeft"   => wa.Bottom - s - margin,
            "BottomRight"  => wa.Bottom - s - margin,
            "CenterTop"    => wa.Top + margin,
            "CenterBottom" => wa.Bottom - s - margin,
            _              => wa.Bottom - s - margin,
        };

        Bounds = new Rectangle(x, y, s, s);

        if (!Visible)
            Show();

        RenderOverlay();
        BringToFront();
        hideTimer.Start();
    }
}
