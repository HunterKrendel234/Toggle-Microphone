using System.Runtime.InteropServices;

namespace MicrophoneToggle;

internal static class NativeMethods
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string DwmApi = "dwmapi.dll";
    private const string Gdi32 = "gdi32.dll";

    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport(User32, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport(User32, CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport(User32)]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport(Kernel32, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport(DwmApi)]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWM_WCP_ROUND = 2;

    internal const int GWL_EXSTYLE = -20;

    [DllImport(User32, SetLastError = true)]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport(User32, SetLastError = true)]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    internal const byte AC_SRC_OVER = 0x00;
    internal const byte AC_SRC_ALPHA = 0x01;
    internal const int ULW_ALPHA = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey,
        ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport(Gdi32)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport(Gdi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport(Gdi32)]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport(Gdi32)]
    internal static extern IntPtr CreateDIBSection(IntPtr hdc, IntPtr pbmi,
        uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

    [DllImport(Gdi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr hObject);
}
