// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing.Drawing2D;

namespace OneCopy;

/// <summary>设置窗口左侧导航的图标类型。</summary>
internal enum NavIcon
{
    Appearance,
    Scanner,
    Exclusions,
    Update,
    About,
}

/// <summary>设置窗口左侧边栏背景（带右侧分隔线）。</summary>
internal sealed class SidebarPanel : PaintedControl
{
    protected override Color PaintBackColor => Theme.Surface;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
    }
}

/// <summary>应用标志：强调色圆角方块 + 白色文件夹图标。</summary>
internal sealed class AppLogo : PaintedControl
{
    protected override Color PaintBackColor => Theme.Surface;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float size = Math.Min(Width, Height);
        if (size <= 1) return;

        Theme.FillRound(g, new RectangleF(0, 0, size, size), size * 0.26f, Theme.Accent);
        var folder = new RectangleF(size * 0.2f, size * 0.26f, size * 0.6f, size * 0.48f);
        Theme.DrawFolderIcon(g, folder, Color.White);
    }
}

/// <summary>设置窗口左侧导航项：图标 + 文本，选中时强调色高亮。</summary>
internal sealed class NavItem : PaintedControl
{
    private bool _hover;
    private bool _selected;

    public NavIcon Icon { get; set; }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    public NavItem()
    {
        Cursor = Cursors.Hand;
        Height = 42;
        TabStop = true;
    }

    /// <summary>未选中 / 未悬停时与侧边栏同色，不出现额外的深色底块。</summary>
    protected override Color PaintBackColor => Theme.Surface;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int pad = Px(2);
        var rect = new RectangleF(pad, pad, Math.Max(0, Width - pad * 2), Math.Max(0, Height - pad * 2));
        if (_selected)
            Theme.FillRound(g, rect, Px(8), Theme.AccentSoft);
        else if (_hover)
            Theme.FillRound(g, rect, Px(8), Theme.SurfaceHover);

        Color foreground = _selected ? Theme.Accent : Theme.TextPrimary;
        Color iconColor = _selected ? Theme.Accent : Theme.TextSecondary;

        var iconRect = new RectangleF(Px(14), (Height - Px(18)) / 2f, Px(18), Px(18));
        DrawIcon(g, iconRect, iconColor);

        var textRect = new Rectangle(Px(44), 0, Math.Max(0, Width - Px(44) - Px(10)), Height);
        TextRenderer.DrawText(
            g,
            Text,
            _selected ? Theme.UIBold : Theme.UI,
            textRect,
            foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    private void DrawIcon(Graphics g, RectangleF rect, Color color)
    {
        switch (Icon)
        {
            case NavIcon.Appearance:
            {
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                using var pen = new Pen(color, Math.Max(1.2f, Px(1.15f)))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                float radius = Px(4.1f);
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
                for (int i = 0; i < 8; i++)
                {
                    double angle = Math.PI / 4 * i;
                    float x1 = cx + (float)Math.Cos(angle) * Px(6.2f);
                    float y1 = cy + (float)Math.Sin(angle) * Px(6.2f);
                    float x2 = cx + (float)Math.Cos(angle) * Px(8.1f);
                    float y2 = cy + (float)Math.Sin(angle) * Px(8.1f);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;
            }

            case NavIcon.Exclusions:
            {
                var points = new[]
                {
                    new PointF(rect.X + rect.Width * 0.08f, rect.Y + rect.Height * 0.16f),
                    new PointF(rect.X + rect.Width * 0.92f, rect.Y + rect.Height * 0.16f),
                    new PointF(rect.X + rect.Width * 0.60f, rect.Y + rect.Height * 0.52f),
                    new PointF(rect.X + rect.Width * 0.60f, rect.Y + rect.Height * 0.90f),
                    new PointF(rect.X + rect.Width * 0.40f, rect.Y + rect.Height * 0.76f),
                    new PointF(rect.X + rect.Width * 0.40f, rect.Y + rect.Height * 0.52f),
                };
                using var brush = new SolidBrush(color);
                g.FillPolygon(brush, points);
                break;
            }

            case NavIcon.Scanner:
            {
                // 芯片图标：圆角方块 + 四周引脚
                using var pen = new Pen(color, Math.Max(1.2f, Px(1.2f)))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                var chip = new RectangleF(
                    rect.X + rect.Width * 0.26f,
                    rect.Y + rect.Height * 0.26f,
                    rect.Width * 0.48f,
                    rect.Height * 0.48f);
                using (GraphicsPath path = Theme.RoundedRect(chip, Px(2.5f)))
                {
                    g.DrawPath(pen, path);
                }

                float pinLength = rect.Width * 0.16f;
                for (int i = 0; i < 3; i++)
                {
                    float offset = rect.Width * (0.36f + i * 0.14f);
                    g.DrawLine(pen, rect.X + offset, chip.Top - pinLength, rect.X + offset, chip.Top);
                    g.DrawLine(pen, rect.X + offset, chip.Bottom, rect.X + offset, chip.Bottom + pinLength);
                }
                for (int i = 0; i < 3; i++)
                {
                    float offset = rect.Height * (0.36f + i * 0.14f);
                    g.DrawLine(pen, chip.Left - pinLength, rect.Y + offset, chip.Left, rect.Y + offset);
                    g.DrawLine(pen, chip.Right, rect.Y + offset, chip.Right + pinLength, rect.Y + offset);
                }
                break;
            }

            case NavIcon.Update:
            {
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                float radius = Px(6.0f);
                using var pen = new Pen(color, Math.Max(1.3f, Px(1.35f)))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                g.DrawArc(pen, cx - radius, cy - radius, radius * 2, radius * 2, -55f, 275f);

                // 箭头
                double endDeg = 220;
                double endRad = endDeg * Math.PI / 180;
                var tip = new PointF(
                    cx + (float)(Math.Cos(endRad) * radius),
                    cy + (float)(Math.Sin(endRad) * radius));
                double tangent = (endDeg + 90) * Math.PI / 180;
                var back = new PointF(
                    tip.X - (float)(Math.Cos(tangent) * Px(4.4f)),
                    tip.Y - (float)(Math.Sin(tangent) * Px(4.4f)));
                float half = Px(2.2f);
                var side1 = new PointF(
                    back.X - (float)(Math.Sin(tangent) * half),
                    back.Y + (float)(Math.Cos(tangent) * half));
                var side2 = new PointF(
                    back.X + (float)(Math.Sin(tangent) * half),
                    back.Y - (float)(Math.Cos(tangent) * half));
                using var brush = new SolidBrush(color);
                g.FillPolygon(brush, new[] { tip, side1, side2 });
                break;
            }

            case NavIcon.About:
            {
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                using var pen = new Pen(color, Math.Max(1.3f, Px(1.3f)))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                float radius = Px(6.4f);
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
                using var brush = new SolidBrush(color);
                float dot = Math.Max(1.1f, Px(1.15f));
                g.FillEllipse(brush, cx - dot, cy - Px(3.9f) - dot, dot * 2, dot * 2);
                g.DrawLine(pen, cx, cy - Px(0.6f), cx, cy + Px(4.0f));
                break;
            }
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }
}

/// <summary>外观设置里的主题预览卡片：缩略图 + 名称，选中时强调色描边。</summary>
internal sealed class ThemePreviewCard : PaintedControl
{
    private readonly record struct Palette(Color Background, Color Surface, Color Accent, Color Border, Color Muted);

    private static readonly Palette LightPalette = new(
        Color.FromArgb(243, 245, 249),
        Color.White,
        Color.FromArgb(46, 107, 222),
        Color.FromArgb(226, 230, 236),
        Color.FromArgb(176, 184, 196));

    private static readonly Palette DarkPalette = new(
        Color.FromArgb(32, 32, 32),
        Color.FromArgb(44, 44, 44),
        Color.FromArgb(76, 141, 255),
        Color.FromArgb(58, 58, 58),
        Color.FromArgb(110, 110, 110));

    private bool _hover;
    private bool _selected;

    public ThemeMode Mode { get; set; }

    public string Caption { get; set; } = string.Empty;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    public ThemePreviewCard()
    {
        Cursor = Cursors.Hand;
        Height = 118;
        TabStop = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var card = new RectangleF(1f, 1f, Math.Max(0, Width - 2f), Math.Max(0, Height - 2f));
        Color fill = _selected ? Theme.AccentSoft : _hover ? Theme.SurfaceHover : Theme.Surface;
        Theme.FillRound(g, card, Px(10), fill);
        Theme.StrokeRound(
            g,
            card,
            Px(10),
            _selected ? Theme.Accent : _hover ? Theme.BorderStrong : Theme.Border,
            _selected ? Math.Max(2f, Px(1.6f)) : 1f);

        var thumb = new RectangleF(Px(10), Px(10), Math.Max(0, Width - Px(20)), Px(72));
        DrawPreview(g, thumb);

        var labelRect = new Rectangle(0, Px(88), Width, Px(22));
        TextRenderer.DrawText(
            g,
            Caption,
            _selected ? Theme.UIBold : Theme.UI,
            labelRect,
            _selected ? Theme.Accent : Theme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private void DrawPreview(Graphics g, RectangleF rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using GraphicsPath clip = Theme.RoundedRect(rect, Px(6));
        GraphicsState state = g.Save();
        g.SetClip(clip, CombineMode.Replace);

        switch (Mode)
        {
            case ThemeMode.Light:
                DrawMiniWindow(g, rect, LightPalette);
                break;

            case ThemeMode.Dark:
                DrawMiniWindow(g, rect, DarkPalette);
                break;

            case ThemeMode.System:
            {
                DrawMiniWindow(g, rect, LightPalette);
                GraphicsState right = g.Save();
                g.SetClip(new RectangleF(rect.X + rect.Width / 2f, rect.Y, rect.Width / 2f, rect.Height), CombineMode.Intersect);
                DrawMiniWindow(g, rect, DarkPalette);
                g.Restore(right);
                break;
            }

            default: // 自定义时间：浅色缩略图 + 时钟
            {
                DrawMiniWindow(g, rect, LightPalette);
                float radius = Math.Min(rect.Width, rect.Height) * 0.24f;
                float cx = rect.X + rect.Width * 0.78f;
                float cy = rect.Y + rect.Height * 0.62f;
                using var pen = new Pen(LightPalette.Accent, Math.Max(1.6f, Px(1.6f)))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                using (var face = new SolidBrush(LightPalette.Surface))
                {
                    g.FillEllipse(face, cx - radius, cy - radius, radius * 2, radius * 2);
                }
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
                g.DrawLine(pen, cx, cy, cx, cy - radius * 0.6f);
                g.DrawLine(pen, cx, cy, cx + radius * 0.5f, cy);
                break;
            }
        }

        g.Restore(state);

        using var border = new Pen(Theme.DarkMode
            ? Color.FromArgb(90, 255, 255, 255)
            : Color.FromArgb(60, 0, 0, 0));
        using GraphicsPath outline = Theme.RoundedRect(rect, Px(6));
        g.DrawPath(border, outline);
    }

    private void DrawMiniWindow(Graphics g, RectangleF rect, Palette palette)
    {
        using (var background = new SolidBrush(palette.Background))
        {
            g.FillRectangle(background, rect);
        }

        float titleHeight = Math.Max(10f, rect.Height * 0.22f);
        using (var surface = new SolidBrush(palette.Surface))
        {
            g.FillRectangle(surface, new RectangleF(rect.X, rect.Y, rect.Width, titleHeight));
        }

        // 标题栏右侧的三个小圆点
        float dotRadius = Math.Max(1.2f, Px(1.4f));
        float dotY = rect.Y + titleHeight / 2f;
        for (int i = 0; i < 3; i++)
        {
            float dotX = rect.Right - Px(10) - i * Px(7);
            using var dot = new SolidBrush(palette.Muted);
            g.FillEllipse(dot, dotX - dotRadius, dotY - dotRadius, dotRadius * 2, dotRadius * 2);
        }

        // 内容：一条强调色短条 + 两条弱化长条
        float left = rect.X + Px(9);
        float contentTop = rect.Y + titleHeight + Px(8);
        float barHeight = Math.Max(3f, Px(4.5f));

        using (var accent = new SolidBrush(palette.Accent))
        {
            g.FillRectangle(accent, new RectangleF(left, contentTop, rect.Width * 0.34f, barHeight));
        }

        using (var muted = new SolidBrush(palette.Muted))
        {
            g.FillRectangle(muted, new RectangleF(left, contentTop + Px(10), rect.Width * 0.62f, barHeight));
            g.FillRectangle(muted, new RectangleF(left, contentTop + Px(20), rect.Width * 0.46f, barHeight));
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }
}

/// <summary>设置页里的可选项按钮（药丸形），选中时强调色高亮。</summary>
internal sealed class OptionChip : PaintedControl
{
    private bool _hover;
    private bool _selected;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    public OptionChip()
    {
        Cursor = Cursors.Hand;
        Height = 38;
        TabStop = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Math.Max(0, Width - 1.5f), Math.Max(0, Height - 1.5f));
        Color fill = _selected ? Theme.AccentSoft : _hover ? Theme.SurfaceHover : Theme.Surface;
        Theme.FillRound(g, rect, Px(9), fill);
        Theme.StrokeRound(
            g,
            rect,
            Px(9),
            _selected ? Theme.Accent : _hover ? Theme.BorderStrong : Theme.Border,
            _selected ? Math.Max(1.6f, Px(1.5f)) : 1f);

        TextRenderer.DrawText(
            g,
            Text,
            _selected ? Theme.UIBold : Theme.UI,
            new Rectangle(0, 0, Width, Height),
            _selected ? Theme.Accent : Theme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }
}

/// <summary>设置页里的开关控件。</summary>
internal sealed class ToggleSwitch : PaintedControl
{
    private bool _hover;
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleSwitch()
    {
        Size = new Size(46, 26);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Color trackColor = !Enabled ? Theme.DisabledFill
            : _checked ? (_hover ? Theme.AccentHover : Theme.Accent)
            : Theme.Track;
        Theme.FillRound(g, track, track.Height / 2f, trackColor);

        float knob = track.Height - Px(6);
        float knobX = _checked ? track.Right - knob - Px(3) : track.Left + Px(3);
        float knobY = track.Top + (track.Height - knob) / 2f;
        using var brush = new SolidBrush(Enabled ? Color.White : Theme.DisabledText);
        g.FillEllipse(brush, knobX, knobY, knob, knob);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Enabled) Checked = !Checked;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter && Enabled)
        {
            e.Handled = true;
            Checked = !Checked;
        }
        base.OnKeyDown(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }
}
