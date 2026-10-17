// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

#if DEBUG
using System.Drawing.Imaging;

namespace OneCopy;

/// <summary>
/// 开发调试用的界面预览渲染器：把窗口显示在屏幕外，用 PrintWindow 截取真实渲染结果。
/// 仅在 Debug 构建中可用。用法：
///   dotnet run -c Debug -- --preview 输出.png [populated|empty|dialog] [dark|light|system]
/// </summary>
internal static class PreviewRenderer
{
    public static void Render(string outputPath, string mode = "populated", string theme = "system")
    {
        ApplyTheme(theme);

        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        if (mode == "dialog")
        {
            RenderDialog(fullPath);
            return;
        }

        using var form = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ShowInTaskbar = false,
        };

        bool empty = mode == "empty";
        form.ApplyPreviewData(empty ? string.Empty : BuildFolder(), empty ? new List<DuplicateGroup>() : BuildDemoGroups());
        form.Show();
        Application.DoEvents();

        using Bitmap bitmap = CaptureWindow(form);
        bitmap.Save(fullPath, ImageFormat.Png);
        form.Close();
    }

    private static void RenderDialog(string fullPath)
    {
        using var owner = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ShowInTaskbar = false,
        };
        owner.Show();

        using Form dialog = owner.CreateScanConfirmDialog(BuildDemoSummary(), BuildFolder());
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(-4000, -4000);
        dialog.ShowInTaskbar = false;
        dialog.Show();
        Application.DoEvents();

        using Bitmap bitmap = CaptureWindow(dialog);
        bitmap.Save(fullPath, ImageFormat.Png);
        dialog.Close();
        owner.Close();
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

    private static void ApplyTheme(string theme)
    {
        if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
        {
            Theme.SetDarkMode(true);
        }
        else if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
        {
            Theme.SetDarkMode(false);
        }
        else
        {
            Theme.InitializeFromSystem();
        }
    }

    internal static string BuildFolder() => @"C:\Users\Public\Downloads\Sample Pictures";

    internal static FileSummary BuildDemoSummary()
    {
        var summary = new FileSummary { TotalFiles = 1284, TotalSize = 2L * 1024 * 1024 * 1024 + 512L * 1024 * 1024 };
        summary.Extensions.Add(new ExtensionGroup { Extension = ".jpg", Count = 842, TotalSize = 1_612_000_000 });
        summary.Extensions.Add(new ExtensionGroup { Extension = ".png", Count = 210, TotalSize = 428_000_000 });
        summary.Extensions.Add(new ExtensionGroup { Extension = ".zip", Count = 6, TotalSize = 320_000_000 });
        summary.Extensions.Add(new ExtensionGroup { Extension = ".mp4", Count = 3, TotalSize = 268_000_000 });
        summary.Extensions.Add(new ExtensionGroup { Extension = ".txt", Count = 64, TotalSize = 1_200_000 });
        summary.Extensions.Add(new ExtensionGroup { Extension = ".pdf", Count = 12, TotalSize = 18_400_000 });
        return summary;
    }

    internal static List<DuplicateGroup> BuildDemoGroups()
    {
        var groups = new List<DuplicateGroup>
        {
            Group("5afa753f5e7c81e1f3fd52b4c9a1d0a6", BuildFolder(),
                ("IMG_2041(1).jpg", 413.87, new DateTime(2026, 10, 5, 15, 44, 0)),
                ("IMG_2041.jpg", 413.87, new DateTime(2026, 10, 5, 15, 7, 0))),

            Group("7e3501fee9e55b7f2a4c98d0b6e3f215", BuildFolder(),
                ("Photo_Cover(1).jpg", 368.06, new DateTime(2026, 10, 5, 15, 33, 0)),
                ("Photo_Cover.jpg", 368.06, new DateTime(2026, 10, 5, 15, 5, 0))),

            Group("03d1c601b432c30e8f5a7d2b9c4e6f18", BuildFolder(),
                ("Scenery_01(1).jpg", 261.07, new DateTime(2026, 10, 5, 15, 42, 0)),
                ("Scenery_01.jpg", 261.07, new DateTime(2026, 10, 5, 15, 6, 0))),

            Group("0c94dd82e98643ee5b1f7c8a2d9e0b34", BuildFolder(),
                ("Scenery_02(1).jpg", 259.84, new DateTime(2026, 10, 5, 15, 43, 0)),
                ("Scenery_02.jpg", 259.84, new DateTime(2026, 10, 5, 15, 6, 0))),

            Group("15f52f1c90b12223a7b8c9d0e1f24356", BuildFolder(),
                ("Travel_03(1).jpg", 246.67, new DateTime(2026, 10, 5, 15, 49, 0)),
                ("Travel_03.jpg", 246.67, new DateTime(2026, 10, 5, 15, 7, 0))),

            Group("4d3694e2579f7bbf6c5d4e3f2a1b0c98", BuildFolder(),
                ("Travel_04(1).jpg", 243.17, new DateTime(2026, 10, 5, 15, 42, 0)),
                ("Travel_04.jpg", 243.17, new DateTime(2026, 10, 5, 15, 6, 0))),
        };

        // 勾选一个文件，方便检查"已勾选 / 部分选中"状态下的视觉效果
        groups[0].Files[1].IsChecked = true;
        return groups;
    }

    private static DuplicateGroup Group(
        string hash,
        string folder,
        params (string Name, double KiloBytes, DateTime Modified)[] files)
    {
        var group = new DuplicateGroup { Hash = hash };
        foreach ((string name, double kiloBytes, DateTime modified) in files)
        {
            group.Files.Add(new FileItem
            {
                FilePath = Path.Combine(folder, name),
                FileSize = (long)(kiloBytes * 1024),
                LastWriteTime = modified,
                Hash = hash,
            });
        }
        return group;
    }
}
#endif
