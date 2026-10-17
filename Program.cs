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

        AppSettings settings = AppSettings.Load();
        Theme.SetDarkMode(settings.ResolveDark(DateTime.Now));

#if DEBUG
        // 开发调试：哈希算法自检（官方测试向量 + 流式/一次性交叉验证）
        //   dotnet run -c Debug -- --hash-test 输出.log
        if (args.Length >= 2 && args[0] == "--hash-test")
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Environment.ExitCode = HashSelfTest.RunConsole(Path.GetFullPath(args[1]));
            return;
        }

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

        // 开发调试：真正显示到屏幕上再屏幕截图，用于检查 DWM 圆角（窗口会短暂出现）
        //   dotnet run -c Debug -- --screen-capture 输出.png [settings|main] [dark|light|system]
        if (args.Length >= 2 && args[0] == "--screen-capture")
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            try
            {
                PreviewRenderer.RenderScreenCapture(
                    args[1],
                    args.Length >= 3 ? args[2] : "settings",
                    args.Length >= 4 ? args[3] : "system");
            }
            catch (Exception ex)
            {
                LogDebugError(ex);
                Environment.ExitCode = 1;
            }
            return;
        }
#endif

        Application.Run(new MainForm(settings));
    }

#if DEBUG
    private static void SmokeTest(string outputPath)
    {
        string fullPath = Path.GetFullPath(outputPath);
        string logPath = fullPath + ".log";
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

        // 自检期间把设置写到输出目录，避免污染真实的 %AppData% 设置
        AppSettings.PathOverride = Path.Combine(
            Path.GetDirectoryName(fullPath) ?? ".",
            "settings-smoke.json");
        try
        {
            File.Delete(AppSettings.PathOverride);
        }
        catch
        {
            // 忽略
        }

        using var form = new MainForm(AppSettings.Load())
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
                HashSelfTest.AppendTo(lines);
            }
            catch (Exception ex)
            {
                lines.Add("hashSelfTest=fail " + ex.Message);
            }

            try
            {
                RunSettingsSelfCheck(form, lines, fullPath);
            }
            catch (Exception ex)
            {
                lines.Add("settings=fail " + ex.Message);
            }

            try
            {
                RunPauseScanCheck(lines, fullPath);
            }
            catch (Exception ex)
            {
                lines.Add("pauseScan=fail " + ex.Message);
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

            try
            {
                if (File.Exists(AppSettings.PathOverride)) File.Delete(AppSettings.PathOverride);
            }
            catch
            {
                // 忽略
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
        lines.Add($"settingsButtonFound={FindCaptionButton(form, CaptionKind.Settings) is not null}");
        lines.Add($"chromeForm={form is ChromeForm}");
        lines.Add($"captionButtons={CaptionKindList(form)}");
        lines.Add($"mainStyles={DescribeChrome(form)}");

        // 扫描确认对话框也应使用同样的圆角窗口框架
        using (Form confirmProbe = form.CreateScanConfirmDialog(
                   PreviewRenderer.BuildDemoSummary(), PreviewRenderer.BuildFolder()))
        {
            confirmProbe.StartPosition = FormStartPosition.Manual;
            confirmProbe.Location = new Point(-4000, -4000);
            confirmProbe.ShowInTaskbar = false;
            confirmProbe.Show();
            Application.DoEvents();
            lines.Add($"confirmStyles={DescribeChrome(confirmProbe)}");
            confirmProbe.Close();
        }

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

        // 扫描中的"暂停 / 继续"与"停止扫描"按钮
        form.ApplyScanningPreviewForTest();
        Application.DoEvents();
        lines.Add(
            $"scanControls pauseVisible={form.PauseButtonVisibleForTest} " +
            $"pauseText={form.PauseButtonTextForTest} " +
            $"stopVisible={form.StopButtonVisibleForTest} stopText={form.StopButtonTextForTest}");
        form.ClearScanningPreviewForTest();
        Application.DoEvents();
        lines.Add(
            $"scanControlsIdle pauseVisible={form.PauseButtonVisibleForTest} " +
            $"stopVisible={form.StopButtonVisibleForTest}");
        form.ApplyPreviewData(PreviewRenderer.BuildFolder(), PreviewRenderer.BuildDemoGroups());
        Application.DoEvents();
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

    /// <summary>列出窗口内所有标题栏按钮，确认没有多余的快速主题切换按钮。</summary>
    private static string CaptionKindList(Control parent)
    {
        var kinds = new List<string>();
        void Walk(Control control)
        {
            foreach (Control child in control.Controls)
            {
                if (child is CaptionButton button) kinds.Add(button.Kind.ToString());
                Walk(child);
            }
        }
        Walk(parent);
        return string.Join(",", kinds);
    }

    /// <summary>窗口框架自检：WS_THICKFRAME / WS_CAPTION 与 DWM 圆角偏好，确认所有窗口外观一致。</summary>
    private static string DescribeChrome(Form form)
    {
        long style = NativeMethods.GetWindowStyle(form.Handle);
        int corner = -1;
        try
        {
            NativeMethods.DwmGetWindowAttribute(
                form.Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, out corner, sizeof(int));
        }
        catch
        {
            // 老系统读不到圆角属性时忽略
        }

        bool thickFrame = (style & 0x00040000) != 0;
        bool caption = (style & 0x00C00000) == 0x00C00000;
        return $"thickFrame={thickFrame} caption={caption} cornerPref={corner}";
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
            lines.Add($"dialog buttonFound={clicked} result={result} chrome={dialog is ChromeForm}");
        }
        finally
        {
            timer.Dispose();
        }
    }

    /// <summary>
    /// 验证设置窗口：导航页切换、外观模式、自定义时间、全局排除扩展名的解析、
    /// 检查更新版本比较、持久化往返与扫描排除效果。
    /// </summary>
    private static void RunSettingsSelfCheck(MainForm form, List<string> lines, string screenshotPath)
    {
        lines.Add($"settingsButtonFound={FindCaptionButton(form, CaptionKind.Settings) is not null}");

        using SettingsForm dialog = form.CreateSettingsDialog();
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(-4000, -4000);
        dialog.ShowInTaskbar = false;
        dialog.Show();
        Application.DoEvents();
        lines.Add(
            $"settingsDialogShown={dialog.Visible} title={dialog.Text} " +
            $"chrome={dialog is ChromeForm} navCount={dialog.NavItemCountForTest}");
        lines.Add($"settingsStyles={DescribeChrome(dialog)}");

        void Shot(Control control, string fileName, string label)
        {
            try
            {
                string? directory = Path.GetDirectoryName(Path.GetFullPath(screenshotPath));
                string path = Path.Combine(directory ?? ".", fileName);
                using Bitmap bitmap = CaptureWindow(control);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                lines.Add($"{label}=ok {bitmap.Width}x{bitmap.Height}");
            }
            catch (Exception ex)
            {
                lines.Add($"{label}=fail " + ex.Message);
            }
        }

        Shot(dialog, "settings.png", "settingsScreenshot");

        // 导航页切换
        dialog.ShowPageForTest(SettingsPage.About);
        Application.DoEvents();
        lines.Add(
            $"settingsPage aboutVisible={dialog.PageVisibleForTest(SettingsPage.About)} " +
            $"title={dialog.PageTitleForTest(SettingsPage.About)} " +
            $"appearanceVisible={dialog.PageVisibleForTest(SettingsPage.Appearance)}");
        Shot(dialog, "settings-about.png", "settingsAboutScreenshot");

        dialog.ShowPageForTest(SettingsPage.Update);
        dialog.PreviewNewerResultForTest();
        Application.DoEvents();
        lines.Add($"settingsPage updateTitle={dialog.PageTitleForTest(SettingsPage.Update)}");
        Shot(dialog, "settings-update.png", "settingsUpdateScreenshot");

        dialog.ShowPageForTest(SettingsPage.Exclusions);
        Application.DoEvents();
        lines.Add(
            $"settingsPage exclusionsTitle={dialog.PageTitleForTest(SettingsPage.Exclusions)} " +
            $"countText={dialog.ExtensionsCountForTest.Replace("\r", "").Replace("\n", "")}");
        Shot(dialog, "settings-exclusions.png", "settingsExclusionsScreenshot");

        // 扫描器页：算法选择、大文件夹提醒、并行数
        dialog.ShowPageForTest(SettingsPage.Scanner);
        Application.DoEvents();
        lines.Add($"settingsPage scannerTitle={dialog.PageTitleForTest(SettingsPage.Scanner)}");
        Shot(dialog, "settings-scanner.png", "settingsScannerScreenshot");
        dialog.SelectAlgorithmForTest(HashAlgorithmKind.Blake3);
        dialog.SetScannerForTest(warnLargeFolder: true, warnGb: 20, autoParallelism: false, parallelCount: 6);
        Application.DoEvents();
        lines.Add(
            $"scannerSelection algorithm={dialog.AlgorithmForTest} " +
            $"desc='{dialog.AlgorithmDescriptionForTest}'");

        dialog.ShowPageForTest(SettingsPage.Appearance);
        Application.DoEvents();

        // 检查更新的版本比较逻辑（不访问网络）
        lines.Add(
            $"updateCompare newer={UpdateChecker.IsNewer("v1.4.2", "1.4.1")} " +
            $"same={UpdateChecker.IsNewer("v1.4.1", "1.4.1")} " +
            $"older={UpdateChecker.IsNewer("v1.4.0", "1.4.1")} " +
            $"parse={UpdateChecker.ParseVersion("v1.4.1+abc")}");

        // 自定义时间 + 全局排除扩展名
        dialog.SelectModeForTest(ThemeMode.Custom);
        dialog.SetTimesForTest(new TimeOnly(19, 0), new TimeOnly(7, 0));
        dialog.SetExtensionsForTest(".tmp\n*.bak, log");
        bool saved = dialog.SaveForTest();
        lines.Add(
            $"settingsSaveValid={saved} mode={dialog.Result.ThemeMode} " +
            $"range={dialog.Result.DarkStart}-{dialog.Result.DarkEnd} " +
            $"exts={string.Join(",", dialog.Result.ExcludedExtensions)}");
        lines.Add(
            $"settingsScannerSaved algorithm={dialog.Result.HashAlgorithm} " +
            $"warn={dialog.Result.WarnLargeFolder} gb={dialog.Result.LargeFolderWarningGb} " +
            $"autoParallel={dialog.Result.AutoParallelism} parallel={dialog.Result.MaxParallelism}");

        AppSettings probe = dialog.Result.Clone();
        lines.Add(
            $"customResolve 20:00={probe.ResolveDark(new DateTime(2026, 1, 1, 20, 0, 0))} " +
            $"12:00={probe.ResolveDark(new DateTime(2026, 1, 1, 12, 0, 0))} " +
            $"06:59={probe.ResolveDark(new DateTime(2026, 1, 1, 6, 59, 0))}");

        // 非法扩展名应被拒绝，并跳到对应页面
        using (SettingsForm invalid = form.CreateSettingsDialog())
        {
            invalid.SetExtensionsForTest(".tmp\n?bad?");
            bool accepted = invalid.SaveForTest();
            lines.Add(
                $"settingsInvalidRejected={!accepted} page={invalid.CurrentPageForTest} " +
                $"error='{invalid.ErrorTextForTest}'");
        }

        // 持久化往返
        bool persisted = dialog.Result.Save();
        AppSettings loaded = AppSettings.Load();
        lines.Add(
            $"settingsPersist={persisted} loadedMode={loaded.ThemeMode} " +
            $"loadedExts={loaded.ExcludedExtensions.Count} loadedRange={loaded.DarkStart}-{loaded.DarkEnd}");
        lines.Add(
            $"scannerPersist algorithm={loaded.HashAlgorithm} warn={loaded.WarnLargeFolder} " +
            $"gb={loaded.LargeFolderWarningGb} autoParallel={loaded.AutoParallelism} parallel={loaded.MaxParallelism}");

        // 应用设置会更新主题与排除集合
        form.ApplySettingsForTest(dialog.Result);
        Application.DoEvents();
        lines.Add(
            $"settingsApplied themeDark={Theme.DarkMode} " +
            $"expected={dialog.Result.ResolveDark(DateTime.Now)} " +
            $"exts={form.ExcludedExtensionsForTest.Count} " +
            $"algorithm={form.HashAlgorithmForTest} warnGb={form.LargeFolderWarningGbForTest}");

        // 外观实时预览：点击卡片立即换肤，不需要保存
        bool beforeDark = Theme.DarkMode;
        dialog.SelectModeWithPreviewForTest(ThemeMode.Light);
        for (int i = 0; i < 30 && Theme.DarkMode; i++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        bool lightPreview = !Theme.DarkMode;

        dialog.SelectModeWithPreviewForTest(ThemeMode.Dark);
        for (int i = 0; i < 30 && !Theme.DarkMode; i++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        bool darkPreview = Theme.DarkMode;
        lines.Add(
            $"themeLivePreview beforeDark={beforeDark} lightPreview={lightPreview} " +
            $"darkPreview={darkPreview} navAfterRebuild={dialog.NavItemCountForTest}");

        RunExclusionScanCheck(form, lines, screenshotPath);
    }

    /// <summary>验证扫描暂停闸门：暂停期间任务不推进，继续后正常完成。</summary>
    private static void RunPauseScanCheck(List<string> lines, string screenshotPath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(screenshotPath));
        string testDir = Path.Combine(directory ?? ".", "pause-test");

        try
        {
            Directory.CreateDirectory(testDir);
            for (int i = 0; i < 6; i++)
            {
                File.WriteAllText(Path.Combine(testDir, $"dup{i}.txt"), "pause-test-content");
            }

            var scanner = new FileScanner();
            scanner.Pause();
            Task<List<DuplicateGroup>> task = scanner.ScanAsync(
                testDir, new Progress<ScanProgress>(), CancellationToken.None);

            // 暂停状态下等待一段时间，任务不应完成
            WaitWithEvents(Task.Delay(300), 3000);
            bool held = !task.IsCompleted;
            bool reportedPaused = scanner.IsPaused;

            scanner.Resume();
            if (!WaitWithEvents(task, 15000))
            {
                lines.Add("pauseScan=timeout");
                return;
            }

            List<DuplicateGroup> groups = task.GetAwaiter().GetResult();
            lines.Add(
                $"pauseScan heldWhilePaused={held} reportedPaused={reportedPaused} " +
                $"groups={groups.Count} files={groups.Sum(g => g.FileCount)}");
        }
        catch (Exception ex)
        {
            lines.Add("pauseScan=fail " + ex.Message);
        }
        finally
        {
            try
            {
                Directory.Delete(testDir, true);
            }
            catch
            {
                // 忽略
            }
        }
    }

    /// <summary>验证全局排除扩展名同时作用于快速统计与哈希扫描。</summary>
    private static void RunExclusionScanCheck(MainForm form, List<string> lines, string screenshotPath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(screenshotPath));
        string testDir = Path.Combine(directory ?? ".", "scan-test");

        try
        {
            Directory.CreateDirectory(testDir);
            File.WriteAllText(Path.Combine(testDir, "keep1.txt"), "same-content");
            File.WriteAllText(Path.Combine(testDir, "keep2.txt"), "same-content");
            File.WriteAllText(Path.Combine(testDir, "skip1.tmp"), "same-content");
            File.WriteAllText(Path.Combine(testDir, "skip2.tmp"), "same-content");

            HashSet<string> excluded = form.ExcludedExtensionsForTest;
            FileSummary all = FileScanner.GetFileSummary(testDir);
            FileSummary filtered = FileScanner.GetFileSummary(testDir, default, excluded);
            lines.Add($"exclusionSummary all={all.TotalFiles} filtered={filtered.TotalFiles}");

            var scanner = new FileScanner { ExcludedExtensions = excluded };
            Task<List<DuplicateGroup>> task = scanner.ScanAsync(
                testDir, new Progress<ScanProgress>(), CancellationToken.None);
            if (!WaitWithEvents(task, 15000))
            {
                lines.Add("exclusionScan=timeout");
                return;
            }

            List<DuplicateGroup> groups = task.GetAwaiter().GetResult();
            lines.Add(
                $"exclusionScan groups={groups.Count} files={groups.Sum(g => g.FileCount)} " +
                $"names={string.Join(",", groups.SelectMany(g => g.Files).Select(f => f.FileName).OrderBy(n => n))}");
        }
        catch (Exception ex)
        {
            lines.Add("exclusionScan=fail " + ex);
        }
        finally
        {
            try
            {
                Directory.Delete(testDir, true);
            }
            catch
            {
                // 忽略
            }
        }
    }

    /// <summary>在自检中等待异步任务完成，同时继续泵消息循环避免死锁。</summary>
    private static bool WaitWithEvents(Task task, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (!task.IsCompleted)
        {
            if (Environment.TickCount64 > deadline) return false;
            Application.DoEvents();
            Thread.Sleep(10);
        }
        return true;
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
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "onecopy-preview-error.log"), ex.ToString());
#endif
}
