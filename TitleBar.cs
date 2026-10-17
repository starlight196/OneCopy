// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing.Drawing2D;

namespace OneCopy;

/// <summary>标题栏按钮类型。</summary>
internal enum CaptionKind
{
    Minimize,
    Maximize,
    Restore,
    Close,
    Settings,
}

/// <summary>窗口圆角 / 深色边框等 DWM 效果。</summary>
internal static class WindowChrome
{
    public static void Apply(Form form)
    {
        if (!form.IsHandleCreated) return;
        try
        {
            int round = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                form.Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            int dark = Theme.DarkMode ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(
                form.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }
        catch
        {
            // 老系统不支持这些 DWM 属性时忽略
        }
    }
}

/// <summary>自绘的窗口标题栏：应用标志、标题、设置与窗口控制按钮，支持拖动与双击最大化。</summary>
internal sealed class TitleBar : PaintedControl
{
    private readonly Form _form;
    private readonly CaptionButton _minimize;
    private readonly CaptionButton _maximize;
    private readonly CaptionButton _close;
    private readonly CaptionButton _settingsButton;
    private readonly ToolTip _toolTip;
    private bool _showMinimize = true;
    private bool _showMaximize = true;
    private bool _showSettings = true;
    private bool _showVersion = true;
    private readonly string _versionText = BuildVersionText();

    private static string BuildVersionText()
    {
        string version = (Application.ProductVersion ?? string.Empty).Trim();
        // 去掉可能的 "+提交哈希" 后缀
        int plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];
        return string.IsNullOrEmpty(version) ? string.Empty : "v" + version;
    }

    public event EventHandler? SettingsRequested;

    public bool ShowMinimize
    {
        get => _showMinimize;
        set
        {
            _showMinimize = value;
            _minimize.Visible = value;
            PerformLayout();
        }
    }

    public bool ShowMaximize
    {
        get => _showMaximize;
        set
        {
            _showMaximize = value;
            _maximize.Visible = value;
            PerformLayout();
        }
    }

    /// <summary>是否显示设置按钮（位于最小化按钮左侧）。</summary>
    public bool ShowSettings
    {
        get => _showSettings;
        set
        {
            _showSettings = value;
            _settingsButton.Visible = value;
            PerformLayout();
        }
    }

    /// <summary>是否在标题后显示版本号。</summary>
    public bool ShowVersion
    {
        get => _showVersion;
        set
        {
            _showVersion = value;
            Invalidate();
        }
    }

    public TitleBar(Form form)
    {
        _form = form;
        _toolTip = new ToolTip { InitialDelay = 400, ReshowDelay = 200 };

        _settingsButton = new CaptionButton(CaptionKind.Settings);
        _minimize = new CaptionButton(CaptionKind.Minimize);
        _maximize = new CaptionButton(CaptionKind.Maximize);
        _close = new CaptionButton(CaptionKind.Close);
        Controls.AddRange(new Control[] { _settingsButton, _minimize, _maximize, _close });

        _toolTip.SetToolTip(_settingsButton, "设置");
        _toolTip.SetToolTip(_minimize, "最小化");
        _toolTip.SetToolTip(_maximize, "最大化 / 还原");
        _toolTip.SetToolTip(_close, "关闭");

        _settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _minimize.Click += (_, _) => _form.WindowState = FormWindowState.Minimized;
        _maximize.Click += (_, _) => ToggleMaximize();
        _close.Click += (_, _) => _form.Close();
    }

    /// <summary>同步最大化状态对应的按钮图标。</summary>
    public void SetMaximized(bool maximized)
    {
        CaptionKind kind = maximized ? CaptionKind.Restore : CaptionKind.Maximize;
        if (_maximize.Kind == kind) return;
        _maximize.Kind = kind;
        _maximize.Invalidate();
    }

    private void ToggleMaximize()
    {
        if (!_showMaximize) return;
        _form.WindowState = _form.WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    protected override Color PaintBackColor => Theme.Surface;

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        int buttonWidth = Px(44);
        int buttonHeight = Px(30);
        int gap = Px(2);
        int top = (Height - Px(1) - buttonHeight) / 2;
        int x = Width - Px(8);

        void Place(CaptionButton button)
        {
            x -= buttonWidth;
            button.Bounds = new Rectangle(x, top, buttonWidth, buttonHeight);
            x -= gap;
        }

        Place(_close);
        if (_showMaximize) Place(_maximize);
        if (_showMinimize) Place(_minimize);

        // 设置按钮（原快速切换主题按钮的位置）
        if (_showSettings)
        {
            x -= Px(6);
            x -= Px(38);
            _settingsButton.Bounds = new Rectangle(x, top, Px(38), buttonHeight);
        }
        else
        {
            _settingsButton.Bounds = Rectangle.Empty;
        }
    }

    /// <summary>标题文本右边界：所有可见按钮中最靠左的位置。</summary>
    private int ContentRightEdge()
    {
        int right = Width;
        bool found = false;
        foreach (CaptionButton button in new[] { _settingsButton, _minimize, _maximize, _close })
        {
            if (!button.Visible || button.Width <= 0) continue;
            if (!found || button.Left < right)
            {
                right = button.Left;
                found = true;
            }
        }
        return (found ? right : Width) - Px(12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Surface);

        using (var border = new Pen(Theme.Border))
        {
            g.DrawLine(border, 0, Height - 1, Width, Height - 1);
        }

        // 应用标志
        float logoSize = Px(22);
        var logo = new RectangleF(Px(16), (Height - Px(1) - logoSize) / 2f, logoSize, logoSize);
        Theme.FillRound(g, logo, Px(6), Theme.Accent);
        var folder = new RectangleF(
            logo.X + Px(4.5f),
            logo.Y + Px(6f),
            logo.Width - Px(9f),
            logo.Height - Px(10.5f));
        Theme.DrawFolderIcon(g, folder, Color.White);

        // 标题
        int titleLeft = Px(48);
        int contentRight = ContentRightEdge();
        var titleRect = new Rectangle(titleLeft, 0, Math.Max(0, contentRight - titleLeft), Height - 1);
        TextRenderer.DrawText(
            g,
            _form.Text,
            Theme.TitleBarFont,
            titleRect,
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        // 版本号（跟随标题右侧，弱化显示）
        if (_showVersion && !string.IsNullOrWhiteSpace(_versionText) && _versionText != "v")
        {
            Size titleSize = TextRenderer.MeasureText(
                g,
                _form.Text,
                Theme.TitleBarFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int versionLeft = titleLeft + titleSize.Width + Px(10);
            int versionRight = contentRight;
            if (versionRight > versionLeft)
            {
                var versionRect = new Rectangle(versionLeft, 0, versionRight - versionLeft, Height - 1);
                TextRenderer.DrawText(
                    g,
                    _versionText,
                    Theme.Small,
                    versionRect,
                    Theme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        if (e.Clicks >= 2)
        {
            ToggleMaximize();
            return;
        }

        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(
            _form.Handle,
            NativeMethods.WM_NCLBUTTONDOWN,
            (IntPtr)NativeMethods.HTCAPTION,
            IntPtr.Zero);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>标题栏上的图标按钮（最小化 / 最大化 / 关闭 / 设置）。</summary>
internal sealed class CaptionButton : PaintedControl
{
    private bool _hover;
    private bool _pressed;

    public CaptionKind Kind { get; set; }

    public CaptionButton(CaptionKind kind)
    {
        Kind = kind;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
    }

    protected override Color PaintBackColor => Theme.Surface;

    /// <summary>模拟点击（供自检/自动化使用）。</summary>
    public void PerformClick() => OnClick(EventArgs.Empty);

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool close = Kind == CaptionKind.Close;
        bool highlight = _hover || _pressed;

        Color hoverBackground = close
            ? (_pressed ? Theme.DangerDown : Theme.DangerHover)
            : (_pressed ? Theme.CaptionPressed : Theme.CaptionHover);

        if (highlight)
        {
            Theme.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Px(7), hoverBackground);
        }

        Color glyph = close && highlight ? Color.White : Theme.TextSecondary;
        using var pen = new Pen(glyph, Math.Max(1.2f, Px(1.15f)))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        float cx = Width / 2f;
        float cy = Height / 2f;

        switch (Kind)
        {
            case CaptionKind.Minimize:
                g.DrawLine(pen, cx - Px(5), cy, cx + Px(5), cy);
                break;

            case CaptionKind.Maximize:
                g.DrawRectangle(pen, cx - Px(5.5f), cy - Px(5.5f), Px(11), Px(11));
                break;

            case CaptionKind.Restore:
                g.DrawRectangle(pen, cx - Px(6), cy - Px(4.5f), Px(9f), Px(8.5f));
                g.DrawLine(pen, cx - Px(3), cy - Px(4.5f), cx - Px(3), cy + Px(2.5f));
                break;

            case CaptionKind.Close:
                g.DrawLine(pen, cx - Px(5), cy - Px(5), cx + Px(5), cy + Px(5));
                g.DrawLine(pen, cx + Px(5), cy - Px(5), cx - Px(5), cy + Px(5));
                break;

            case CaptionKind.Settings:
                DrawSettingsGlyph(g, glyph, highlight ? hoverBackground : Theme.Surface, cx, cy);
                break;
        }
    }

    /// <summary>齿轮图标：8 个梯形齿 + 实心圆盘 + 中心镂空，小尺寸下也能明确识别。</summary>
    private void DrawSettingsGlyph(Graphics g, Color glyph, Color background, float cx, float cy)
    {
        float root = Px(4.9f);    // 齿根半径
        float tip = Px(8.1f);     // 齿顶半径
        float halfTip = Px(1.75f); // 齿顶半宽
        float halfRoot = halfTip * 1.35f;

        using var path = new GraphicsPath(FillMode.Winding);
        for (int i = 0; i < 8; i++)
        {
            double angle = Math.PI / 4 * i;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double tx = -sin;
            double ty = cos;

            PointF Root(float offset) => new(
                (float)(cx + cos * root + tx * offset),
                (float)(cy + sin * root + ty * offset));
            PointF Tip(float offset) => new(
                (float)(cx + cos * tip + tx * offset),
                (float)(cy + sin * tip + ty * offset));

            path.AddPolygon(new[]
            {
                Root(-halfRoot),
                Tip(-halfTip),
                Tip(halfTip),
                Root(halfRoot),
            });
        }

        path.AddEllipse(cx - root, cy - root, root * 2, root * 2);

        using (var brush = new SolidBrush(glyph))
        {
            g.FillPath(brush, path);
        }

        float hole = Px(2.5f);
        using (var brush = new SolidBrush(background))
        {
            g.FillEllipse(brush, cx - hole, cy - hole, hole * 2, hole * 2);
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
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
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
}
