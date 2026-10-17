// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace OneCopy;

/// <summary>
/// 现代风格的重复文件分组列表：自绘行、圆角复选框、两行文件信息、
/// 分组折叠、自定义细滚动条，深浅色主题自适应。
/// </summary>
internal sealed class DuplicateListView : PaintedControl
{
    private sealed class Row
    {
        public bool IsHeader;
        public DuplicateGroup Group = null!;
        public FileItem? File;
        public int Top;
        public int Height;
        public int IndexInGroup;
        public bool Alternate;
    }

    private readonly List<Row> _rows = new();
    private readonly HashSet<DuplicateGroup> _collapsed = new();
    private List<DuplicateGroup> _groups = new();
    private int _contentHeight;
    private int _scrollY;
    private int _hoverIndex = -1;
    private bool _thumbHover;
    private bool _draggingThumb;
    private int _dragOffset;
    private string _emptyTitle = "还没有重复文件";
    private string _emptyHint = "选择一个文件夹，开始查找内容完全相同的文件";

    public event EventHandler? CheckedChanged;

    public DuplicateListView()
    {
        BackColor = Color.Transparent;
    }

    public IReadOnlyList<DuplicateGroup> Groups => _groups;

    public bool HasGroups => _groups.Count > 0;

    public int CheckedFileCount => _groups.Sum(g => g.Files.Count(f => f.IsChecked));

    public long CheckedFileBytes => _groups.Sum(g => g.Files.Where(f => f.IsChecked).Sum(f => f.FileSize));

    /// <summary>用新的扫描结果刷新列表。</summary>
    public void SetGroups(IReadOnlyList<DuplicateGroup> groups, bool keepCollapsed = false)
    {
        _groups = groups.ToList();
        if (!keepCollapsed) _collapsed.Clear();
        _scrollY = 0;
        _hoverIndex = -1;
        _draggingThumb = false;
        Rebuild();
        Invalidate();
    }

    /// <summary>设置空状态提示文案。</summary>
    public void SetEmptyState(string title, string hint)
    {
        _emptyTitle = title;
        _emptyHint = hint;
        Invalidate();
    }

    /// <summary>外部修改勾选状态后重绘。</summary>
    public void RefreshState() => Invalidate();

    // ===== 行布局 =====

    private void Rebuild()
    {
        _rows.Clear();
        int y = Px(2);
        foreach (DuplicateGroup group in _groups)
        {
            _rows.Add(new Row
            {
                IsHeader = true,
                Group = group,
                Top = y,
                Height = Px(40),
            });
            y += Px(40);

            if (!_collapsed.Contains(group))
            {
                for (int i = 0; i < group.Files.Count; i++)
                {
                    _rows.Add(new Row
                    {
                        Group = group,
                        File = group.Files[i],
                        Top = y,
                        Height = Px(54),
                        IndexInGroup = i,
                        Alternate = i % 2 == 1,
                    });
                    y += Px(54);
                }
            }
            y += Px(10); // 组间距
        }

        _contentHeight = y;
        _scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);
    }

    private bool ScrollbarVisible => _contentHeight > ClientSize.Height;

    private int MaxScrollY => Math.Max(0, _contentHeight - ClientSize.Height);

    private int ScrollbarZoneWidth => Px(14);

    private Rectangle ThumbBounds
    {
        get
        {
            if (!ScrollbarVisible) return Rectangle.Empty;
            int viewHeight = ClientSize.Height;
            int thumbHeight = Math.Max(Px(34), (int)((long)viewHeight * viewHeight / Math.Max(1, _contentHeight)));
            thumbHeight = Math.Min(thumbHeight, viewHeight);
            int maxTop = viewHeight - thumbHeight;
            int top = MaxScrollY <= 0 ? 0 : (int)((long)_scrollY * maxTop / MaxScrollY);
            int thumbWidth = Px(_draggingThumb || _thumbHover ? 8 : 5);
            int zoneLeft = Width - ScrollbarZoneWidth;
            int x = zoneLeft + (ScrollbarZoneWidth - thumbWidth) / 2;
            return new Rectangle(x, top, thumbWidth, thumbHeight);
        }
    }

    private Rectangle ScrollbarZone => ScrollbarVisible
        ? new Rectangle(Width - ScrollbarZoneWidth, 0, ScrollbarZoneWidth, ClientSize.Height)
        : Rectangle.Empty;

    private int HitTest(int y)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            int top = _rows[i].Top - _scrollY;
            if (y >= top && y < top + _rows[i].Height) return i;
        }
        return -1;
    }

    private void SetScroll(int value)
    {
        int clamped = Math.Clamp(value, 0, MaxScrollY);
        if (clamped == _scrollY) return;
        _scrollY = clamped;
        Invalidate();
    }

    // ===== 绘制 =====

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Surface);

        if (_groups.Count == 0)
        {
            DrawEmptyState(g);
            return;
        }

        int viewWidth = ClientSize.Width - (ScrollbarVisible ? ScrollbarZoneWidth : 0);
        int viewHeight = ClientSize.Height;

        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i];
            int y = row.Top - _scrollY;
            if (y + row.Height < 0) continue;
            if (y > viewHeight) break;

            var rect = new Rectangle(Px(6), y, Math.Max(0, viewWidth - Px(12)), row.Height);
            if (row.IsHeader) DrawGroupRow(g, row, rect, i == _hoverIndex);
            else DrawFileRow(g, row, rect, i == _hoverIndex);
        }

        DrawScrollbar(g);
    }

    private void DrawScrollbar(Graphics g)
    {
        Rectangle thumb = ThumbBounds;
        if (thumb.IsEmpty) return;
        Color color = _draggingThumb || _thumbHover ? Theme.ScrollThumbHover : Theme.ScrollThumb;
        Theme.FillRound(g, thumb, thumb.Width / 2f, color);
    }

    private void DrawEmptyState(Graphics g)
    {
        int cx = Width / 2;
        int cy = Height / 2 - Px(24);
        float r = Px(26);

        using var pen = new Pen(Theme.BorderStrong, Math.Max(1.5f, Px(2)));
        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
        g.DrawLine(pen, cx + r * 0.62f, cy + r * 0.62f, cx + r * 1.18f, cy + r * 1.18f);

        var titleRect = new Rectangle(0, (int)(cy + r + Px(22)), Width, Px(32));
        var hintRect = new Rectangle(0, titleRect.Bottom + Px(6), Width, Px(24));

        TextRenderer.DrawText(g, _emptyTitle, Theme.Title, titleRect, Theme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, _emptyHint, Theme.Small, hintRect, Theme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private void DrawGroupRow(Graphics g, Row row, Rectangle rect, bool hovered)
    {
        var box = new RectangleF(rect.X, rect.Y + Px(2), rect.Width, rect.Height - Px(4));
        Color background = hovered ? Theme.GroupRowHover : Theme.GroupRow;
        Theme.FillRound(g, box, Px(9), background);

        // 折叠箭头
        float cx = box.X + Px(20);
        float cy = box.Y + box.Height / 2f;
        float s = Px(5);
        PointF[] arrow = _collapsed.Contains(row.Group)
            ? new[] { new PointF(cx - s * 0.55f, cy - s), new PointF(cx + s * 0.75f, cy), new PointF(cx - s * 0.55f, cy + s) }
            : new[] { new PointF(cx - s, cy - s * 0.55f), new PointF(cx + s, cy - s * 0.55f), new PointF(cx, cy + s * 0.75f) };
        using (var arrowBrush = new SolidBrush(Theme.TextSecondary))
        {
            g.FillPolygon(arrowBrush, arrow);
        }

        // 复选框
        var checkRect = new RectangleF(
            box.X + Px(42), box.Y + (box.Height - Px(18)) / 2f, Px(18), Px(18));
        DrawCheckBox(g, checkRect, GroupCheckState(row.Group));

        // 哈希值
        float textX = box.X + Px(74);
        string hashText = row.Group.HashShort.ToUpperInvariant();
        Size hashSize = TextRenderer.MeasureText(g, hashText, Theme.UIBold, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        var hashRect = new Rectangle((int)textX, (int)box.Y, Math.Min((int)(box.Right - textX), hashSize.Width + Px(4)), (int)box.Height);
        TextRenderer.DrawText(g, hashText, Theme.UIBold, hashRect, Theme.TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        // 右侧统计
        string meta = $"{row.Group.FileCount} 个文件 · {row.Group.WastedSpaceDisplay} 可释放";
        var metaRect = new Rectangle((int)(textX + hashSize.Width + Px(14)), (int)box.Y,
            Math.Max(0, (int)(box.Right - textX - hashSize.Width - Px(30))), (int)box.Height);
        TextRenderer.DrawText(g, meta, Theme.Small, metaRect, Theme.TextSecondary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private void DrawFileRow(Graphics g, Row row, Rectangle rect, bool hovered)
    {
        FileItem file = row.File!;
        Color background = file.IsChecked ? Theme.RowChecked
            : hovered ? Theme.RowHover
            : row.Alternate ? Theme.RowAlternate
            : Theme.Surface;

        if (background != Theme.Surface)
        {
            var box = new RectangleF(rect.X + Px(30), rect.Y + Px(1), Math.Max(0, rect.Width - Px(30)), rect.Height - Px(2));
            Theme.FillRound(g, box, Px(7), background);
        }

        var checkRect = new RectangleF(rect.X + Px(42), rect.Y + Px(10), Px(18), Px(18));
        DrawCheckBox(g, checkRect, file.IsChecked);

        float textX = rect.X + Px(74);
        float rightPad = Px(16);
        float sizeW = Px(96);
        float dateW = Px(120);
        float textRight = rect.Right - rightPad - Math.Max(sizeW, dateW);

        var nameRect = new Rectangle((int)textX, rect.Y + Px(7), Math.Max(0, (int)(textRight - textX)), Px(22));
        TextRenderer.DrawText(g, file.FileName, Theme.UI, nameRect, Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        var pathRect = new Rectangle((int)textX, rect.Y + Px(29), Math.Max(0, (int)(textRight - textX)), Px(20));
        TextRenderer.DrawText(g, file.DirectoryName, Theme.Small, pathRect, Theme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        var sizeRect = new Rectangle((int)(rect.Right - rightPad - sizeW), rect.Y + Px(7), (int)sizeW, Px(22));
        TextRenderer.DrawText(g, file.FileSizeDisplay, Theme.Small, sizeRect, Theme.TextSecondary,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        var dateRect = new Rectangle((int)(rect.Right - rightPad - dateW), rect.Y + Px(29), (int)dateW, Px(20));
        TextRenderer.DrawText(g, file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), Theme.Small, dateRect, Theme.TextMuted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        // 组内分隔线
        if (row.IndexInGroup < row.Group.Files.Count - 1)
        {
            using var separator = new Pen(Theme.Separator);
            g.DrawLine(separator, textX, rect.Bottom - Px(1), rect.Right - rightPad, rect.Bottom - Px(1));
        }
    }

    private static void DrawCheckBox(Graphics g, RectangleF box, bool? state)
    {
        using GraphicsPath path = Theme.RoundedRect(box, box.Width * 0.26f);
        if (state == false)
        {
            using var fill = new SolidBrush(Theme.DarkMode ? Theme.Input : Theme.Surface);
            g.FillPath(fill, path);
            using var border = new Pen(Theme.BorderStrong, Math.Max(1f, box.Width * 0.07f));
            g.DrawPath(border, path);
            return;
        }

        using (var fill = new SolidBrush(Theme.Accent))
        {
            g.FillPath(fill, path);
        }

        using var mark = new Pen(Color.White, Math.Max(1.6f, box.Width * 0.13f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        if (state == true)
        {
            var points = new[]
            {
                new PointF(box.Left + box.Width * 0.26f, box.Top + box.Height * 0.53f),
                new PointF(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.70f),
                new PointF(box.Left + box.Width * 0.74f, box.Top + box.Height * 0.31f),
            };
            g.DrawLines(mark, points);
        }
        else // 部分选中
        {
            g.DrawLine(mark,
                box.Left + box.Width * 0.27f, box.Top + box.Height * 0.5f,
                box.Left + box.Width * 0.73f, box.Top + box.Height * 0.5f);
        }
    }

    private static bool? GroupCheckState(DuplicateGroup group)
    {
        int checkedCount = group.Files.Count(f => f.IsChecked);
        if (checkedCount == 0) return false;
        if (checkedCount == group.Files.Count) return true;
        return null;
    }

    // ===== 交互 =====

    protected override void OnMouseEnter(EventArgs e)
    {
        Focus();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        bool changed = false;
        if (_hoverIndex != -1) { _hoverIndex = -1; changed = true; }
        if (_thumbHover) { _thumbHover = false; changed = true; }
        if (changed) Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_draggingThumb)
        {
            Rectangle thumb = ThumbBounds;
            int viewHeight = ClientSize.Height;
            int travel = Math.Max(1, viewHeight - thumb.Height);
            int newTop = e.Y - _dragOffset;
            int maxScroll = MaxScrollY;
            SetScroll(maxScroll == 0 ? 0 : (int)((long)newTop * maxScroll / travel));
            return;
        }

        bool overScrollbar = ScrollbarVisible && ScrollbarZone.Contains(e.Location);
        bool thumbHover = overScrollbar && ThumbBounds.Contains(e.Location);
        bool thumbChanged = thumbHover != _thumbHover;
        _thumbHover = thumbHover;

        int index = HitTest(e.Y);
        if (overScrollbar) index = -1;
        Cursor = index >= 0 || thumbHover ? Cursors.Hand : Cursors.Default;
        bool rowChanged = index != _hoverIndex;
        _hoverIndex = index;

        if (rowChanged || thumbChanged) Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left) return;

        // 先处理滚动条
        if (ScrollbarVisible && ScrollbarZone.Contains(e.Location))
        {
            Rectangle thumb = ThumbBounds;
            if (thumb.Contains(e.Location))
            {
                _draggingThumb = true;
                _dragOffset = e.Y - thumb.Top;
            }
            else
            {
                int page = Math.Max(Px(40), ClientSize.Height - Px(40));
                SetScroll(ThumbBounds.Contains(e.Location) ? _scrollY : (e.Y < ThumbBounds.Top ? _scrollY - page : _scrollY + page));
            }
            Invalidate();
            return;
        }

        int index = HitTest(e.Y);
        if (index < 0) return;

        Row row = _rows[index];
        if (row.IsHeader)
        {
            int rowTop = row.Top - _scrollY;
            var chevronHit = new Rectangle(Px(8), rowTop + Px(6), Px(32), row.Height - Px(12));
            if (chevronHit.Contains(e.Location))
            {
                if (!_collapsed.Remove(row.Group)) _collapsed.Add(row.Group);
                Rebuild();
            }
            else
            {
                ToggleGroup(row.Group);
            }
        }
        else if (row.File is not null)
        {
            row.File.IsChecked = !row.File.IsChecked;
        }

        Invalidate();
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_draggingThumb)
        {
            _draggingThumb = false;
            Invalidate();
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (ScrollbarVisible && ScrollbarZone.Contains(e.Location)) return;
        int index = HitTest(e.Y);
        if (index < 0 || _rows[index].IsHeader) return;
        RevealInExplorer(_rows[index].File!.FilePath);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!ScrollbarVisible) return;
        SetScroll(_scrollY - e.Delta / 120 * Px(56));
    }

    private void ToggleGroup(DuplicateGroup group)
    {
        bool allChecked = group.Files.Count > 0 && group.Files.All(f => f.IsChecked);
        foreach (FileItem file in group.Files)
        {
            file.IsChecked = !allChecked;
        }
    }

    private static void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // 资源管理器启动失败时静默忽略
        }
    }
}

/// <summary>列表上方的列头。</summary>
internal sealed class ListHeaderPanel : PaintedControl
{
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Surface);

        using (var pen = new Pen(Theme.Border))
        {
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }

        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var leftRect = new Rectangle(Px(74), 0, Math.Max(0, Width / 2), Height - 1);
        var rightRect = new Rectangle(0, 0, Math.Max(0, Width - Px(16)), Height - 1);
        TextRenderer.DrawText(g, "重复组 / 文件", Theme.SmallBold, leftRect, Theme.TextSecondary,
            flags | TextFormatFlags.Left);
        TextRenderer.DrawText(g, "大小 · 修改时间", Theme.Small, rightRect, Theme.TextMuted,
            flags | TextFormatFlags.Right);
    }
}
