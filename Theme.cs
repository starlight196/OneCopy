// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing.Drawing2D;

namespace OneCopy;

/// <summary>
/// 现代主题：统一调色板（浅色 / 深色）、字体与圆角绘图工具。
/// 颜色是动态属性，切换深浅色后所有自绘控件在重绘时自动使用新配色。
/// </summary>
internal static class Theme
{
    public static bool DarkMode { get; private set; }

    /// <summary>读取系统当前的应用主题（true = 深色）；读不到时按浅色处理。</summary>
    public static bool ReadSystemDarkMode()
    {
        try
        {
            object? value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetDarkMode(bool dark) => DarkMode = dark;

    // ===== 浅色调色板 =====
    private static readonly Color LBackground = Color.FromArgb(243, 245, 249);
    private static readonly Color LSurface = Color.FromArgb(255, 255, 255);
    private static readonly Color LInput = Color.FromArgb(255, 255, 255);
    private static readonly Color LSurfaceHover = Color.FromArgb(244, 247, 252);
    private static readonly Color LSurfacePressed = Color.FromArgb(231, 236, 244);
    private static readonly Color LBorder = Color.FromArgb(226, 230, 236);
    private static readonly Color LBorderStrong = Color.FromArgb(199, 206, 216);
    private static readonly Color LSeparator = Color.FromArgb(240, 242, 246);
    private static readonly Color LTextPrimary = Color.FromArgb(23, 26, 32);
    private static readonly Color LTextSecondary = Color.FromArgb(95, 103, 116);
    private static readonly Color LTextMuted = Color.FromArgb(150, 158, 170);
    private static readonly Color LAccent = Color.FromArgb(46, 107, 222);
    private static readonly Color LAccentHover = Color.FromArgb(36, 90, 196);
    private static readonly Color LAccentDown = Color.FromArgb(28, 72, 160);
    private static readonly Color LAccentSoft = Color.FromArgb(236, 243, 255);
    private static readonly Color LDanger = Color.FromArgb(212, 47, 47);
    private static readonly Color LDangerHover = Color.FromArgb(188, 33, 33);
    private static readonly Color LDangerDown = Color.FromArgb(160, 26, 26);
    private static readonly Color LGroupRow = Color.FromArgb(245, 248, 253);
    private static readonly Color LGroupRowHover = Color.FromArgb(237, 242, 250);
    private static readonly Color LRowAlternate = Color.FromArgb(251, 252, 254);
    private static readonly Color LRowHover = Color.FromArgb(243, 247, 253);
    private static readonly Color LRowChecked = Color.FromArgb(236, 243, 255);
    private static readonly Color LTrack = Color.FromArgb(232, 236, 242);
    private static readonly Color LDisabledFill = Color.FromArgb(233, 236, 241);
    private static readonly Color LDisabledText = Color.FromArgb(163, 170, 181);
    private static readonly Color LCaptionHover = Color.FromArgb(234, 237, 242);
    private static readonly Color LCaptionPressed = Color.FromArgb(220, 225, 232);
    private static readonly Color LScrollThumb = Color.FromArgb(200, 206, 215);
    private static readonly Color LScrollThumbHover = Color.FromArgb(172, 180, 192);

    // ===== 深色调色板（Win11 深色风格） =====
    private static readonly Color DBackground = Color.FromArgb(32, 32, 32);
    private static readonly Color DSurface = Color.FromArgb(39, 39, 39);
    private static readonly Color DInput = Color.FromArgb(45, 45, 45);
    private static readonly Color DSurfaceHover = Color.FromArgb(55, 55, 55);
    private static readonly Color DSurfacePressed = Color.FromArgb(66, 66, 66);
    private static readonly Color DBorder = Color.FromArgb(60, 60, 60);
    private static readonly Color DBorderStrong = Color.FromArgb(84, 84, 84);
    private static readonly Color DSeparator = Color.FromArgb(50, 50, 50);
    private static readonly Color DTextPrimary = Color.FromArgb(242, 242, 242);
    private static readonly Color DTextSecondary = Color.FromArgb(178, 178, 178);
    private static readonly Color DTextMuted = Color.FromArgb(130, 130, 130);
    private static readonly Color DAccent = Color.FromArgb(76, 141, 255);
    private static readonly Color DAccentHover = Color.FromArgb(111, 163, 255);
    private static readonly Color DAccentDown = Color.FromArgb(58, 116, 224);
    private static readonly Color DAccentSoft = Color.FromArgb(35, 50, 72);
    private static readonly Color DDanger = Color.FromArgb(229, 83, 75);
    private static readonly Color DDangerHover = Color.FromArgb(244, 110, 100);
    private static readonly Color DDangerDown = Color.FromArgb(196, 62, 55);
    private static readonly Color DGroupRow = Color.FromArgb(46, 46, 46);
    private static readonly Color DGroupRowHover = Color.FromArgb(53, 53, 53);
    private static readonly Color DRowAlternate = Color.FromArgb(42, 42, 42);
    private static readonly Color DRowHover = Color.FromArgb(49, 49, 49);
    private static readonly Color DRowChecked = Color.FromArgb(35, 50, 72);
    private static readonly Color DTrack = Color.FromArgb(58, 58, 58);
    private static readonly Color DDisabledFill = Color.FromArgb(52, 52, 52);
    private static readonly Color DDisabledText = Color.FromArgb(112, 112, 112);
    private static readonly Color DCaptionHover = Color.FromArgb(56, 56, 56);
    private static readonly Color DCaptionPressed = Color.FromArgb(70, 70, 70);
    private static readonly Color DScrollThumb = Color.FromArgb(88, 88, 88);
    private static readonly Color DScrollThumbHover = Color.FromArgb(120, 120, 120);

    // ===== 动态颜色 =====
    public static Color Background => DarkMode ? DBackground : LBackground;
    public static Color Surface => DarkMode ? DSurface : LSurface;
    public static Color Input => DarkMode ? DInput : LInput;
    public static Color SurfaceHover => DarkMode ? DSurfaceHover : LSurfaceHover;
    public static Color SurfacePressed => DarkMode ? DSurfacePressed : LSurfacePressed;
    public static Color Border => DarkMode ? DBorder : LBorder;
    public static Color BorderStrong => DarkMode ? DBorderStrong : LBorderStrong;
    public static Color Separator => DarkMode ? DSeparator : LSeparator;
    public static Color TextPrimary => DarkMode ? DTextPrimary : LTextPrimary;
    public static Color TextSecondary => DarkMode ? DTextSecondary : LTextSecondary;
    public static Color TextMuted => DarkMode ? DTextMuted : LTextMuted;
    public static Color Accent => DarkMode ? DAccent : LAccent;
    public static Color AccentHover => DarkMode ? DAccentHover : LAccentHover;
    public static Color AccentDown => DarkMode ? DAccentDown : LAccentDown;
    public static Color AccentSoft => DarkMode ? DAccentSoft : LAccentSoft;
    public static Color Danger => DarkMode ? DDanger : LDanger;
    public static Color DangerHover => DarkMode ? DDangerHover : LDangerHover;
    public static Color DangerDown => DarkMode ? DDangerDown : LDangerDown;
    public static Color GroupRow => DarkMode ? DGroupRow : LGroupRow;
    public static Color GroupRowHover => DarkMode ? DGroupRowHover : LGroupRowHover;
    public static Color RowAlternate => DarkMode ? DRowAlternate : LRowAlternate;
    public static Color RowHover => DarkMode ? DRowHover : LRowHover;
    public static Color RowChecked => DarkMode ? DRowChecked : LRowChecked;
    public static Color Track => DarkMode ? DTrack : LTrack;
    public static Color DisabledFill => DarkMode ? DDisabledFill : LDisabledFill;
    public static Color DisabledText => DarkMode ? DDisabledText : LDisabledText;
    public static Color CaptionHover => DarkMode ? DCaptionHover : LCaptionHover;
    public static Color CaptionPressed => DarkMode ? DCaptionPressed : LCaptionPressed;
    public static Color ScrollThumb => DarkMode ? DScrollThumb : LScrollThumb;
    public static Color ScrollThumbHover => DarkMode ? DScrollThumbHover : LScrollThumbHover;

    // ===== 字体 =====
    private static readonly string Family = ResolveFamily();
    private static readonly Dictionary<string, Font> Cache = new();
    private static Icon? _appIcon;

    public static Font UI => Font(9.75f, FontStyle.Regular);
    public static Font UIBold => Font(9.75f, FontStyle.Bold);
    public static Font Small => Font(8.75f, FontStyle.Regular);
    public static Font SmallBold => Font(8.75f, FontStyle.Bold);
    public static Font Title => Font(13.5f, FontStyle.Bold);
    public static Font PageTitle => Font(15.5f, FontStyle.Bold);
    public static Font TitleBarFont => Font(10f, FontStyle.Bold);

    public static Icon AppIcon => _appIcon ??= CreateAppIcon();

    private static string ResolveFamily()
    {
        foreach (string name in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" })
        {
            try
            {
                using var probe = new Font(name, 9f);
                if (string.Equals(probe.Name, name, StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            catch
            {
                // 忽略，继续尝试下一个字体
            }
        }
        return SystemFonts.MessageBoxFont?.Name ?? "Segoe UI";
    }

    private static Font Font(float size, FontStyle style)
    {
        string key = $"{Family}|{size}|{(int)style}";
        if (!Cache.TryGetValue(key, out var font))
        {
            font = new Font(Family, size, style, GraphicsUnit.Point);
            Cache[key] = font;
        }
        return font;
    }

    private static Icon CreateAppIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            FillRound(g, new RectangleF(2f, 2f, 60f, 60f), 14f, Color.FromArgb(46, 107, 222));

            using var white = new SolidBrush(Color.White);
            using (GraphicsPath tab = RoundedRect(new RectangleF(14f, 21f, 17f, 10f), 3f))
            {
                g.FillPath(white, tab);
            }
            using (GraphicsPath body = RoundedRect(new RectangleF(14f, 27f, 36f, 21f), 4f))
            {
                g.FillPath(white, body);
            }
        }

        IntPtr handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    // ===== 绘图工具 =====
    public static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0.5f || rect.Width <= 0 || rect.Height <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        float d = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    public static void FillRound(Graphics g, RectangleF rect, float radius, Color color)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || color.A == 0) return;
        using GraphicsPath path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRound(Graphics g, RectangleF rect, float radius, Color color, float width = 1f)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || color.A == 0) return;
        using GraphicsPath path = RoundedRect(rect, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    /// <summary>在矩形内绘制"文件夹"小图标。</summary>
    public static void DrawFolderIcon(Graphics g, RectangleF rect, Color color)
    {
        using var brush = new SolidBrush(color);

        using (GraphicsPath tab = RoundedRect(
            new RectangleF(rect.X, rect.Y + rect.Height * 0.06f, rect.Width * 0.46f, rect.Height * 0.42f),
            rect.Height * 0.1f))
        {
            g.FillPath(brush, tab);
        }

        using GraphicsPath body = RoundedRect(
            new RectangleF(rect.X, rect.Y + rect.Height * 0.24f, rect.Width, rect.Height * 0.66f),
            rect.Height * 0.12f);
        g.FillPath(brush, body);
    }
}

/// <summary>
/// 所有自绘控件的基类：双缓冲、透明背景、支持离屏渲染（WM_PRINTCLIENT）。
/// </summary>
internal abstract class PaintedControl : Control
{
    protected PaintedControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
    }

    /// <summary>当前 DPI 相对 96 的缩放比例。</summary>
    protected float DpiScale => DeviceDpi / 96f;

    /// <summary>逻辑像素 → 设备像素。</summary>
    protected int Px(float value) => (int)Math.Round(value * DpiScale);

    /// <summary>
    /// 控件自绘背景色。默认透明（跟随父容器），子类可覆盖为固定颜色。
    /// 离屏渲染（WM_PRINT）时父容器不会帮子控件补背景，因此需要自己填充。
    /// </summary>
    protected virtual Color PaintBackColor => Color.Transparent;

    /// <summary>解析实际用于填充的背景色：优先 PaintBackColor，其次沿父链取第一个不透明背景色。</summary>
    protected Color ResolvePaintBackColor()
    {
        if (PaintBackColor != Color.Transparent) return PaintBackColor;
        for (Control? parent = Parent; parent is not null; parent = parent.Parent)
        {
            if (!parent.BackColor.IsEmpty && parent.BackColor != Color.Transparent)
                return parent.BackColor;
        }
        return Theme.Background;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Color background = ResolvePaintBackColor();
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.ClipRectangle);
    }

    protected override void WndProc(ref Message m)
    {
        // 离屏渲染（DrawToBitmap / WM_PRINT）时，用户绘制控件不会被默认处理，需要手动响应。
        if (m.Msg == 0x0318) // WM_PRINTCLIENT
        {
            using var g = Graphics.FromHdc(m.WParam);
            using var args = new PaintEventArgs(g, ClientRectangle);
            OnPaintBackground(args);
            OnPaint(args);
            return;
        }

        base.WndProc(ref m);
    }
}

/// <summary>按钮视觉样式。</summary>
internal enum ButtonKind
{
    Primary,
    Secondary,
    Danger,
    Ghost,
}

/// <summary>现代扁平按钮：圆角、悬停/按下反馈、多种配色。</summary>
internal sealed class ModernButton : PaintedControl, IButtonControl
{
    private bool _hover;
    private bool _pressed;

    public ButtonKind Kind { get; set; } = ButtonKind.Secondary;

    /// <summary>圆角半径（逻辑像素）。</summary>
    public int Radius { get; set; } = 8;

    /// <summary>作为对话框默认按钮时的返回值。</summary>
    public DialogResult DialogResult { get; set; } = DialogResult.None;

    public void NotifyDefault(bool value)
    {
    }

    public void PerformClick() => OnClick(EventArgs.Empty);

    public ModernButton()
    {
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool enabled = Enabled;
        Color fill, stroke, text;

        switch (Kind)
        {
            case ButtonKind.Primary:
                fill = !enabled ? Theme.DisabledFill
                    : _pressed ? Theme.AccentDown
                    : _hover ? Theme.AccentHover
                    : Theme.Accent;
                stroke = Color.Transparent;
                text = enabled ? Color.White : Theme.DisabledText;
                break;

            case ButtonKind.Danger:
                fill = !enabled ? Theme.DisabledFill
                    : _pressed ? Theme.DangerDown
                    : _hover ? Theme.DangerHover
                    : Theme.Danger;
                stroke = Color.Transparent;
                text = enabled ? Color.White : Theme.DisabledText;
                break;

            case ButtonKind.Ghost:
                fill = !enabled ? Color.Transparent
                    : _pressed ? Theme.SurfacePressed
                    : _hover ? Theme.SurfaceHover
                    : Color.Transparent;
                stroke = Color.Transparent;
                text = enabled ? Theme.TextSecondary : Theme.DisabledText;
                break;

            default: // Secondary
                fill = !enabled ? Theme.DisabledFill
                    : _pressed ? Theme.SurfacePressed
                    : _hover ? Theme.SurfaceHover
                    : Theme.Surface;
                stroke = enabled ? Theme.Border : Theme.Separator;
                text = enabled ? Theme.TextPrimary : Theme.DisabledText;
                break;
        }

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = Px(Radius);
        if (fill.A > 0) Theme.FillRound(g, rect, radius, fill);
        if (stroke.A > 0) Theme.StrokeRound(g, rect, radius, stroke);

        Font font = Kind is ButtonKind.Primary or ButtonKind.Danger ? Theme.UIBold : Theme.UI;
        TextRenderer.DrawText(
            g,
            Text,
            font,
            Rectangle.Round(rect),
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        // 普通 Button 会自动把 DialogResult 交给所在对话框并关闭它，
        // 自绘按钮需要自己实现同样的行为。
        if (DialogResult == DialogResult.None) return;
        Form? form = FindForm();
        if (form is null) return;
        form.DialogResult = DialogResult;
        if (form.Visible) form.Close();
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
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
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

/// <summary>圆角进度条，支持不确定态（无限滚动动画）。</summary>
internal sealed class ModernProgressBar : PaintedControl
{
    private readonly System.Windows.Forms.Timer _timer;
    private float _phase;
    private int _value;
    private int _maximum = 100;
    private bool _indeterminate;

    public Color BarColor { get; set; } = Color.Empty;

    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, 0, Math.Max(0, _maximum));
            if (_value == v) return;
            _value = v;
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            int v = Math.Max(1, value);
            if (_maximum == v) return;
            _maximum = v;
            if (_value > _maximum) _value = _maximum;
            Invalidate();
        }
    }

    public bool Indeterminate
    {
        get => _indeterminate;
        set
        {
            if (_indeterminate == value) return;
            _indeterminate = value;
            if (value)
            {
                _phase = 0;
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
            Invalidate();
        }
    }

    public ModernProgressBar()
    {
        _timer = new System.Windows.Forms.Timer { Interval = 30 };
        _timer.Tick += (_, _) =>
        {
            _phase += 0.022f;
            if (_phase > 1f) _phase -= 1f;
            Invalidate();
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int barHeight = Math.Max(Px(8), 6);
        float top = (Height - barHeight) / 2f;
        float width = Math.Max(0, Width - 1);
        var track = new RectangleF(0.5f, top, width, barHeight);
        Theme.FillRound(g, track, barHeight / 2f, Theme.Track);

        Color barColor = BarColor.IsEmpty ? Theme.Accent : BarColor;

        if (_indeterminate)
        {
            float segW = Math.Max(barHeight * 2f, width * 0.28f);
            float x = (width + segW) * _phase - segW;
            var seg = new RectangleF(x, top, segW, barHeight);
            Theme.FillRound(g, seg, barHeight / 2f, barColor);
            return;
        }

        float ratio = _value / (float)_maximum;
        float fillW = width * ratio;
        if (fillW < barHeight) return; // 太小就不画，保持轨道干净
        var fill = new RectangleF(0.5f, top, fillW, barHeight);
        Theme.FillRound(g, fill, barHeight / 2f, barColor);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>带描边、圆角与柔和阴影的卡片容器。</summary>
internal class CardPanel : PaintedControl
{
    public int CornerRadius { get; set; } = 12;

    /// <summary>阴影扩散范围（逻辑像素），由容器内部留白实现。</summary>
    public int ShadowSpread { get; set; }

    public Color FillColor { get; set; } = Color.Empty;
    public Color StrokeColor { get; set; } = Color.Empty;

    public override Rectangle DisplayRectangle
    {
        get
        {
            Rectangle rect = base.DisplayRectangle;
            int spread = Px(ShadowSpread);
            if (spread <= 0) return rect;
            return new Rectangle(
                rect.X + spread,
                rect.Y + spread,
                Math.Max(0, rect.Width - spread * 2),
                Math.Max(0, rect.Height - spread * 2));
        }
    }

    protected override Color PaintBackColor => Theme.Background;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill = FillColor.IsEmpty ? Theme.Surface : FillColor;
        Color stroke = StrokeColor.IsEmpty ? Theme.Border : StrokeColor;
        int spread = Px(ShadowSpread);
        int radius = Px(CornerRadius);

        var full = new RectangleF(0f, 0f, Width, Height);
        var card = spread > 0
            ? new RectangleF(spread, spread, Width - spread * 2f, Height - spread * 2f)
            : new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        if (spread > 0)
        {
            // 多层低透明度圆角矩形模拟柔和阴影
            for (int layer = 3; layer >= 1; layer--)
            {
                float grow = spread * layer / 3f;
                var shadow = RectangleF.Inflate(card, grow * 0.45f, grow * 0.45f);
                shadow.Offset(0f, Px(1f) * layer);
                using GraphicsPath path = Theme.RoundedRect(shadow, radius + grow * 0.45f);
                using var brush = new SolidBrush(Color.FromArgb(Theme.DarkMode ? 26 : 12, 0, 0, 0));
                g.FillPath(brush, path);
            }
        }

        Theme.FillRound(g, card, radius, fill);
        Theme.StrokeRound(g, card, radius, stroke);
        _ = full;
    }
}

/// <summary>圆角路径输入框（只读展示当前扫描目录）。</summary>
internal sealed class PathBox : PaintedControl
{
    private readonly Label _label;
    private string _value = string.Empty;
    private const string Placeholder = "尚未选择文件夹";

    public string Value
    {
        get => _value;
        set
        {
            _value = value ?? string.Empty;
            bool empty = string.IsNullOrWhiteSpace(_value);
            _label.Text = empty ? Placeholder : _value;
            _label.ForeColor = empty ? Theme.TextMuted : Theme.TextPrimary;
        }
    }

    public PathBox()
    {
        BackColor = Color.Transparent;
        _label = new Label
        {
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            ForeColor = Theme.TextMuted,
            Font = Theme.UI,
            Text = Placeholder,
        };
        Controls.Add(_label);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int left = Px(42);
        _label.Bounds = new Rectangle(
            left,
            0,
            Math.Max(0, Width - left - Px(14)),
            Math.Max(0, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRound(g, rect, Px(8), Theme.Input);
        Theme.StrokeRound(g, rect, Px(8), Theme.Border);

        float iconSize = Px(18);
        var iconRect = new RectangleF(Px(15), (Height - iconSize * 0.82f) / 2f, iconSize, iconSize * 0.82f);
        Theme.DrawFolderIcon(g, iconRect, Theme.Accent);
    }
}
