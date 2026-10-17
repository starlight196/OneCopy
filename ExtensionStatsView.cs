// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing.Drawing2D;

namespace OneCopy;

/// <summary>按扩展名统计的现代绘制表格，替代原生 ListView（深色模式下外观也一致）。</summary>
internal sealed class ExtensionStatsView : PaintedControl
{
    private List<ExtensionGroup> _items = new();

    public void SetItems(IReadOnlyList<ExtensionGroup> items)
    {
        _items = items.ToList();
        Invalidate();
    }

    protected override Color PaintBackColor => Theme.Surface;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Surface);

        int paddingX = Px(12);
        int headerHeight = Px(30);
        int rowHeight = Px(30);
        int countWidth = Px(90);
        int sizeWidth = Px(120);

        const TextFormatFlags baseFlags =
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        // 列头
        var headerRect = new Rectangle(0, 0, Width, headerHeight);
        TextRenderer.DrawText(g, "类型", Theme.SmallBold,
            new Rectangle(paddingX, 0, Math.Max(0, Width - paddingX * 2 - countWidth - sizeWidth), headerHeight),
            Theme.TextSecondary, baseFlags | TextFormatFlags.Left);
        TextRenderer.DrawText(g, "数量", Theme.SmallBold,
            new Rectangle(Width - paddingX - countWidth - sizeWidth, 0, countWidth, headerHeight),
            Theme.TextSecondary, baseFlags | TextFormatFlags.Right);
        TextRenderer.DrawText(g, "大小", Theme.SmallBold,
            new Rectangle(Width - paddingX - sizeWidth, 0, sizeWidth, headerHeight),
            Theme.TextSecondary, baseFlags | TextFormatFlags.Right);

        using (var pen = new Pen(Theme.Separator))
        {
            g.DrawLine(pen, 0, headerHeight - 1, Width, headerHeight - 1);
        }

        int available = Height - headerHeight;
        int maxRows = Math.Max(0, available / rowHeight);
        if (maxRows == 0) return;

        bool hasMore = _items.Count > maxRows;
        int rowCount = hasMore ? maxRows - 1 : Math.Min(_items.Count, maxRows);

        for (int i = 0; i < rowCount; i++)
        {
            ExtensionGroup item = _items[i];
            int top = headerHeight + i * rowHeight;
            if (i % 2 == 1)
            {
                using var brush = new SolidBrush(Theme.RowAlternate);
                g.FillRectangle(brush, 0, top, Width, rowHeight);
            }

            TextRenderer.DrawText(g, string.IsNullOrEmpty(item.Extension) ? "(无扩展名)" : item.Extension,
                Theme.UI,
                new Rectangle(paddingX, top, Math.Max(0, Width - paddingX * 2 - countWidth - sizeWidth), rowHeight),
                Theme.TextPrimary, baseFlags | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, item.Count.ToString("n0"), Theme.Small,
                new Rectangle(Width - paddingX - countWidth - sizeWidth, top, countWidth, rowHeight),
                Theme.TextSecondary, baseFlags | TextFormatFlags.Right);
            TextRenderer.DrawText(g, item.TotalSizeDisplay, Theme.Small,
                new Rectangle(Width - paddingX - sizeWidth, top, sizeWidth, rowHeight),
                Theme.TextSecondary, baseFlags | TextFormatFlags.Right);
        }

        if (hasMore)
        {
            int top = headerHeight + rowCount * rowHeight;
            TextRenderer.DrawText(g, $"… 还有 {_items.Count - rowCount} 种类型", Theme.Small,
                new Rectangle(paddingX, top, Math.Max(0, Width - paddingX * 2), rowHeight),
                Theme.TextMuted, baseFlags | TextFormatFlags.Left);
        }
    }
}
