// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace OneCopy;

/// <summary>
/// 统一的窗口基类：无边框 + DWM 圆角 / 深色边框 + 自绘标题栏拖动。
/// 主窗口和所有对话框都继承它，保证每个窗口的圆角、阴影和外边框完全一致。
/// </summary>
public abstract class ChromeForm : Form
{
    protected ChromeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        Font = Theme.UI;
        AutoScaleMode = AutoScaleMode.None; // 所有尺寸由 Px() 手动按 DPI 缩放
        DoubleBuffered = true;
        Icon = Theme.AppIcon;
    }

    /// <summary>是否允许拖动窗口边缘缩放（主窗口为 true，固定尺寸对话框为 false）。</summary>
    protected virtual bool AllowResize => false;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX
            // 保留系统框架（用于 DWM 圆角、阴影与贴边分屏），标题栏由 WM_NCCALCSIZE 去掉。
            cp.Style |= 0x00C00000 | 0x00040000 | 0x00080000 | 0x00020000 | 0x00010000;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
        {
            // 去掉系统标题栏：客户区直接铺满整个窗口
            if (NativeMethods.IsZoomed(Handle))
            {
                // 最大化时把客户区对齐到工作区，避免多出一圈屏幕外边框
                Rectangle workingArea = Screen.FromHandle(Handle).WorkingArea;
                var rect = Marshal.PtrToStructure<NativeMethods.RECT>(m.LParam);
                rect.Left = workingArea.Left;
                rect.Top = workingArea.Top;
                rect.Right = workingArea.Right;
                rect.Bottom = workingArea.Bottom;
                Marshal.StructureToPtr(rect, m.LParam, false);
            }

            m.Result = IntPtr.Zero;
            return;
        }

        if (m.Msg == NativeMethods.WM_NCHITTEST)
        {
            base.WndProc(ref m);

            if (!AllowResize)
            {
                // 固定尺寸对话框：不允许拖边框缩放
                m.Result = (IntPtr)NativeMethods.HTCLIENT;
                return;
            }

            if ((int)m.Result != NativeMethods.HTCLIENT) return;
            if (WindowState != FormWindowState.Normal) return;

            // 客户区铺满窗口后系统不再提供缩放边框，这里自行判断边缘
            int lParam = m.LParam.ToInt32();
            var screenPoint = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
            Point client = PointToClient(screenPoint);
            int border = Math.Max(6, ScalePx(5));
            bool left = client.X < border;
            bool right = client.X >= ClientSize.Width - border;
            bool top = client.Y < border;
            bool bottom = client.Y >= ClientSize.Height - border;

            if (left && top) m.Result = (IntPtr)NativeMethods.HTTOPLEFT;
            else if (right && top) m.Result = (IntPtr)NativeMethods.HTTOPRIGHT;
            else if (left && bottom) m.Result = (IntPtr)NativeMethods.HTBOTTOMLEFT;
            else if (right && bottom) m.Result = (IntPtr)NativeMethods.HTBOTTOMRIGHT;
            else if (left) m.Result = (IntPtr)NativeMethods.HTLEFT;
            else if (right) m.Result = (IntPtr)NativeMethods.HTRIGHT;
            else if (top) m.Result = (IntPtr)NativeMethods.HTTOP;
            else if (bottom) m.Result = (IntPtr)NativeMethods.HTBOTTOM;
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>逻辑像素 → 设备像素。</summary>
    protected int ScalePx(float value) => (int)Math.Round(value * DeviceDpi / 96f);
}

/// <summary>固定尺寸的无边框对话框（与主窗口使用同一套圆角外观）。</summary>
internal sealed class ChromeDialog : ChromeForm
{
}
