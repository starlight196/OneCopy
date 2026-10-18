// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace OneCopy;

/// <summary>Win32 / DWM 互操作，用于自绘标题栏的无边框窗口。</summary>
internal static class NativeMethods
{
    // 窗口消息
    public const int WM_NCCALCSIZE = 0x0083;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_NCHITTEST = 0x0084;

    // 命中测试
    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;

    // 系统度量
    public const int SM_CXSIZEFRAME = 32;
    public const int SM_CYSIZEFRAME = 33;
    public const int SM_CXPADDEDBORDER = 92;

    // DWM 属性
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    /// <summary>WM_NCHITTEST 返回值。</summary>
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>读取 DWM 窗口属性（例如圆角偏好）。</summary>
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    /// <summary>GWL_STYLE 窗口样式索引。</summary>
    public const int GWL_STYLE = -16;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    /// <summary>读取窗口样式位（WS_THICKFRAME / WS_CAPTION 等），用于自检窗口框架。</summary>
    public static long GetWindowStyle(IntPtr hWnd) => IntPtr.Size == 8
        ? GetWindowLongPtr64(hWnd, GWL_STYLE).ToInt64()
        : GetWindowLong32(hWnd, GWL_STYLE);

    /// <summary>PrintWindow 默认行为：通过 WM_PRINT 让窗口自己绘制内容（能正确渲染自绘控件）。</summary>
    public const uint PW_DEFAULT = 0x00000000;

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);

    // ===== Shell：在资源管理器中定位文件 =====
    // 通过 PIDL（Shell 项标识符）把路径交给系统，不经过 explorer.exe 命令行；
    // 文件名里的空格、&、引号等字符只作为文件系统项，不会被当作命令或参数解析。

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr ILCreateFromPathW(string pszPath);

    [DllImport("shell32.dll")]
    public static extern void ILFree(IntPtr pidl);

    /// <summary>
    /// 打开文件所在文件夹并选中该项。cidl = 0 时 pidlFolder 指向单个完整项，
    /// 系统会打开其父文件夹并选中它（MSDN：SHOpenFolderAndSelectItems）。
    /// </summary>
    [DllImport("shell32.dll")]
    public static extern int SHOpenFolderAndSelectItems(
        IntPtr pidlFolder, uint cidl, IntPtr apidl, uint dwFlags);
}
