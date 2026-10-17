// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

namespace OneCopy;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Theme.InitializeFromSystem();

#if DEBUG
        // 开发调试：离屏渲染界面预览图，例如
        //   dotnet run -c Debug -- --preview 输出.png [populated|empty|dialog] [dark|light|system]
        if (args.Length >= 2 && args[0] == "--preview")
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            try
            {
                PreviewRenderer.Render(
                    args[1],
                    args.Length >= 3 ? args[2] : "populated",
                    args.Length >= 4 ? args[3] : "system");
            }
            catch (Exception ex)
            {
                LogDebugError(ex);
                Environment.ExitCode = 1;
            }
            return;
        }

        // 开发调试：真实启动一次窗口（移到屏幕外，短暂显示后自动关闭并截图），
        //   dotnet run -c Debug -- --smoke 输出.png
        if (args.Length >= 2 && args[0] == "--smoke")
        {
            SmokeTest(args[1]);
            return;
        }
#endif

        Application.Run(new MainForm());
    }

#if DEBUG
    private static void SmokeTest(string outputPath)
    {
        string fullPath = Path.GetFullPath(outputPath);
        string logPath = fullPath + ".log";
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

        using var form = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ShowInTaskbar = false,
        };
        form.ApplyPreviewData(PreviewRenderer.BuildFolder(), PreviewRenderer.BuildDemoGroups());

        var timer = new System.Windows.Forms.Timer { Interval = 1200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var lines = new List<string>
            {
                $"handleCreated={form.IsHandleCreated}",
                $"visible={form.Visible}",
                $"dpi={form.DeviceDpi}",
                $"size={form.Size}",
                $"client={form.ClientSize}",
                $"themeDark={Theme.DarkMode}",
            };

            try
            {
                RunChromeSelfCheck(form, lines, fullPath);
            }
            catch (Exception ex)
            {
                lines.Add("chrome=fail " + ex.Message);
            }

            try
            {
                RunInteractionSelfCheck(form, lines);
            }
            catch (Exception ex)
            {
                lines.Add("interaction=fail " + ex.Message);
            }

            try
            {
                RunDialogSelfCheck(form, lines);
            }
            catch (Exception ex)
            {
                lines.Add("dialog=fail " + ex.Message);
            }

            try
            {
                RunThemeToggleSelfCheck(form, lines);
            }
            catch (Exception ex)
            {
                lines.Add("themeToggle=fail " + ex.Message);
            }

            try
            {
                using Bitmap bitmap = CaptureWindow(form);
                string? directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                bitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                lines.Add($"screenshot=ok size={bitmap.Width}x{bitmap.Height}");
            }
            catch (Exception ex)
            {
                lines.Add("screenshot=fail " + ex.Message);
            }

            try
            {
                File.WriteAllLines(logPath, lines);
            }
            catch
            {
                // 忽略日志写入失败
            }
            form.Close();
        };

        timer.Start();
        try
        {
            Application.Run(form);
        }
        catch (Exception ex)
        {
            LogDebugError(ex);
            Environment.ExitCode = 1;
        }
        finally
        {
            timer.Dispose();
        }
    }

    /// <summary>验证无边框窗口的可调整边框（WM_NCHITTEST）、最大化布局与标题栏按钮。</summary>
    private static void RunChromeSelfCheck(MainForm form, List<string> lines, string screenshotPath)
    {
        Point origin = form.PointToScreen(Point.Empty);

        long HitTest(int screenX, int screenY)
        {
            var lParam = (IntPtr)unchecked((screenY << 16) | (screenX & 0xFFFF));
            return NativeMethods.SendMessage(form.Handle, NativeMethods.WM_NCHITTEST, IntPtr.Zero, lParam).ToInt64();
        }

        lines.Add($"hitTestLeft={HitTest(origin.X + 2, origin.Y + form.Height / 2)}");
        lines.Add($"hitTestTop={HitTest(origin.X + form.Width / 2, origin.Y + 2)}");
        lines.Add($"hitTestCorner={HitTest(origin.X + 2, origin.Y + 2)}");
        lines.Add($"themeButtonFound={FindCaptionButton(form, CaptionKind.Theme) is not null}");

        form.WindowState = FormWindowState.Maximized;
        Application.DoEvents();
        Rectangle working = Screen.FromControl(form).WorkingArea;
        lines.Add($"maxClient={form.ClientSize} workingArea={working.Size} state={form.WindowState}");

        try
        {
            string maxPath = Path.ChangeExtension(screenshotPath, ".max.png");
            using Bitmap maxBitmap = CaptureWindow(form);
            maxBitmap.Save(maxPath, System.Drawing.Imaging.ImageFormat.Png);
            lines.Add($"maxScreenshot=ok {maxBitmap.Width}x{maxBitmap.Height}");
        }
        catch (Exception ex)
        {
            lines.Add("maxScreenshot=fail " + ex.Message);
        }

        // 点击标题栏的"还原"按钮，验证自绘标题栏按钮可用
        CaptionButton? restore = FindCaptionButton(form, CaptionKind.Restore);
        restore?.PerformClick();
        Application.DoEvents();
        lines.Add($"restoreButtonFound={restore is not null} stateAfterRestoreClick={form.WindowState} client={form.ClientSize}");
    }

    private static CaptionButton? FindCaptionButton(Control parent, CaptionKind kind)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is CaptionButton button && button.Kind == kind) return button;
            CaptionButton? found = FindCaptionButton(child, kind);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// 用 Win32 消息模拟鼠标点击，验证"点击分组复选框 → 模型同步 → 统计与删除按钮更新"。
    /// </summary>
    private static void RunInteractionSelfCheck(MainForm form, List<string> lines)
    {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        var list = (DuplicateListView)typeof(MainForm).GetField("_list", Flags)!.GetValue(form)!;
        var deleteButton = (ModernButton)typeof(MainForm).GetField("_btnDelete", Flags)!.GetValue(form)!;
        var statsLabel = (Label)typeof(MainForm).GetField("_lblStats", Flags)!.GetValue(form)!;

        lines.Add($"clickBefore checked={list.CheckedFileCount} deleteEnabled={deleteButton.Enabled}");

        // 计算第一个分组头复选框的屏幕内坐标（按控件 DPI 缩放）
        float scale = list.DeviceDpi / 96f;
        int rowX = (int)Math.Round(6 * scale);
        int checkLeft = rowX + (int)Math.Round(42 * scale);
        int checkWidth = (int)Math.Round(18 * scale);
        int headerTop = (int)Math.Round(2 * scale);
        int headerHeight = (int)Math.Round(40 * scale);
        int x = checkLeft + checkWidth / 2;
        int y = headerTop + headerHeight / 2;
        var lParam = (IntPtr)unchecked((y << 16) | (x & 0xFFFF));

        NativeMethods.SendMessage(list.Handle, 0x0201, (IntPtr)1, lParam);  // WM_LBUTTONDOWN
        NativeMethods.SendMessage(list.Handle, 0x0202, IntPtr.Zero, lParam); // WM_LBUTTONUP
        Application.DoEvents();

        lines.Add($"clickAfter checked={list.CheckedFileCount} deleteEnabled={deleteButton.Enabled} stats='{statsLabel.Text}'");
    }

    /// <summary>
    /// 打开"扫描确认"对话框并自动点击"开始扫描"，验证自绘按钮的 DialogResult 行为。
    /// </summary>
    private static void RunDialogSelfCheck(MainForm form, List<string> lines)
    {
        using Form dialog = form.CreateScanConfirmDialog(
            PreviewRenderer.BuildDemoSummary(),
            PreviewRenderer.BuildFolder());
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(-4000, -4000);
        dialog.ShowInTaskbar = false;

        bool clicked = false;
        var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) =>
        {
            if (clicked) return;
            ModernButton? start = FindButton(dialog, "开始扫描");
            if (start is null) return;
            clicked = true;
            timer.Stop();
            start.PerformClick();
        };
        timer.Start();

        try
        {
            DialogResult result = dialog.ShowDialog(form);
            lines.Add($"dialog buttonFound={clicked} result={result}");
        }
        finally
        {
            timer.Dispose();
        }
    }

    /// <summary>验证主题切换会重建界面且不丢失扫描结果。</summary>
    private static void RunThemeToggleSelfCheck(MainForm form, List<string> lines)
    {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        bool before = Theme.DarkMode;
        form.ToggleThemeForTest();
        Application.DoEvents();

        var list = (DuplicateListView)typeof(MainForm).GetField("_list", Flags)!.GetValue(form)!;
        lines.Add($"themeAfterToggle={Theme.DarkMode} groupsAfter={list.Groups.Count}");

        form.ToggleThemeForTest();
        Application.DoEvents();
        lines.Add($"themeRestored={Theme.DarkMode == before}");
    }

    private static Bitmap CaptureWindow(Control control)
    {
        var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        using Graphics g = Graphics.FromImage(bitmap);
        IntPtr hdc = g.GetHdc();
        try
        {
            NativeMethods.PrintWindow(control.Handle, hdc, NativeMethods.PW_DEFAULT);
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }
        return bitmap;
    }

    private static ModernButton? FindButton(Control parent, string text)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is ModernButton button && button.Text == text) return button;
            ModernButton? found = FindButton(child, text);
            if (found is not null) return found;
        }
        return null;
    }

    private static void LogDebugError(Exception ex) =>
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "dff-preview-error.log"), ex.ToString());
#endif
}
