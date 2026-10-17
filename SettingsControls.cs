// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing.Drawing2D;
using System.Globalization;

namespace OneCopy;

/// <summary>
/// 24 小时制时间输入框：圆角输入框 + 内嵌无边框 TextBox。
/// 支持 ↑ / ↓ 每 15 分钟调整，Enter 提交；非法输入时红色描边。
/// </summary>
internal sealed class TimeField : PaintedControl
{
    private static readonly string[] Formats = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" };

    private readonly TextBox _input;
    private TimeOnly _value;
    private bool _hover;
    private bool _focused;
    private bool _invalid;

    /// <summary>时间被成功提交（失焦 / Enter / ↑↓ 调整）时触发，用于实时预览外观。</summary>
    public event EventHandler? ValueCommitted;

    public TimeField()
    {
        Cursor = Cursors.IBeam;

        _input = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Input,
            ForeColor = Theme.TextPrimary,
            Font = Theme.UI,
            TextAlign = HorizontalAlignment.Center,
            MaxLength = 5,
        };
        Controls.Add(_input);

        _input.GotFocus += (_, _) =>
        {
            _focused = true;
            Invalidate();
        };
        _input.LostFocus += (_, _) =>
        {
            _focused = false;
            CommitOrRevert();
            Invalidate();
        };
        _input.TextChanged += (_, _) =>
        {
            _invalid = false;
            Invalidate();
        };
        _input.KeyDown += OnInputKeyDown;

        Height = 34;
    }

    public TimeOnly Value
    {
        get => _value;
        set
        {
            _value = value;
            _input.Text = Format(value);
            _invalid = false;
            Invalidate();
        }
    }

    /// <summary>读取并提交当前输入；格式不正确时返回 false 并显示红色描边。</summary>
    public bool TryCommit(out TimeOnly value)
    {
        if (TryParse(_input.Text, out value))
        {
            bool changed = value != _value;
            _value = value;
            _input.Text = Format(value);
            _invalid = false;
            Invalidate();
            if (changed) ValueCommitted?.Invoke(this, EventArgs.Empty);
            return true;
        }

        _invalid = true;
        Invalidate();
        value = _value;
        return false;
    }

    /// <summary>失焦时：能解析就提交，否则恢复上一次的有效值。</summary>
    public void CommitOrRevert()
    {
        bool changed = false;
        if (TryParse(_input.Text, out TimeOnly value))
        {
            changed = value != _value;
            _value = value;
        }
        _input.Text = Format(_value);
        _invalid = false;
        Invalidate();
        if (changed) ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_input is null) return;
        int inputHeight = Math.Max(Px(16), _input.PreferredHeight);
        _input.Bounds = new Rectangle(
            Px(8),
            Math.Max(0, (Height - inputHeight) / 2),
            Math.Max(0, Width - Px(16)),
            inputHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRound(g, rect, Px(8), Theme.Input);

        Color border = _invalid ? Theme.Danger
            : _focused ? Theme.Accent
            : _hover ? Theme.BorderStrong
            : Theme.Border;
        Theme.StrokeRound(g, rect, Px(8), border, _focused || _invalid ? Math.Max(1.5f, Px(1.3f)) : 1f);
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
        _input.Focus();
        _input.SelectAll();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Up or Keys.Down)
        {
            if (!TryParse(_input.Text, out TimeOnly value)) value = _value;
            value = value.AddMinutes(e.KeyCode == Keys.Up ? 15 : -15);
            Value = new TimeOnly(value.Hour, value.Minute);
            _input.SelectAll();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            TryCommit(out _);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private static string Format(TimeOnly value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static bool TryParse(string? text, out TimeOnly value) =>
        TimeOnly.TryParseExact(
            (text ?? string.Empty).Trim(),
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out value);
}

/// <summary>
/// 圆角多行输入框：圆角描边容器 + 内嵌无边框多行 TextBox。
/// 不显示系统滚动条以保持深色外观一致，支持滚轮 / 键盘翻页。
/// </summary>
internal sealed class MultilineInput : PaintedControl
{
    private readonly TextBox _input;
    private bool _focused;
    private bool _hover;

    public MultilineInput()
    {
        _input = new TextBox
        {
            Multiline = true,
            BorderStyle = BorderStyle.None,
            AcceptsReturn = true,
            AcceptsTab = false,
            WordWrap = false,
            ScrollBars = ScrollBars.None,
            BackColor = Theme.Input,
            ForeColor = Theme.TextPrimary,
            Font = Theme.UI,
        };
        Controls.Add(_input);

        _input.GotFocus += (_, _) =>
        {
            _focused = true;
            Invalidate();
        };
        _input.LostFocus += (_, _) =>
        {
            _focused = false;
            Invalidate();
        };
        _input.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _input.Text;
        set => _input.Text = value ?? string.Empty;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        _input.Bounds = new Rectangle(
            Px(12),
            Px(10),
            Math.Max(0, Width - Px(24)),
            Math.Max(0, Height - Px(20)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRound(g, rect, Px(8), Theme.Input);

        Color border = _focused ? Theme.Accent
            : _hover ? Theme.BorderStrong
            : Theme.Border;
        Theme.StrokeRound(g, rect, Px(8), border, _focused ? Math.Max(1.5f, Px(1.3f)) : 1f);
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
        _input.Focus();
    }
}
