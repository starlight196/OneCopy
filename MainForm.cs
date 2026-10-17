// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace OneCopy;

public sealed class MainForm : Form
{
    private enum KeepMode { First, Newest, SelectAll, DeselectAll }

    // 设计稿基准尺寸（逻辑像素 @96 DPI）
    private const int DesignWidth = 1240;
    private const int DesignHeight = 860;
    private const int DesignMinWidth = 1000;
    private const int DesignMinHeight = 700;
    private const int TitleBarHeight = 48;

    private List<DuplicateGroup> _allGroups = new();
    private CancellationTokenSource? _scanCts;
    private bool _isScanning;
    private bool _summarizing;
    private bool _closing;
    private float _scale = 1f;

    private TitleBar _titleBar = null!;
    private ModernButton _btnSelectFolder = null!;
    private PathBox _pathBox = null!;
    private DuplicateListView _list = null!;
    private ModernProgressBar _progress = null!;
    private Label _lblProgress = null!;
    private ModernButton _btnCancel = null!;
    private ModernButton _btnKeepFirst = null!;
    private ModernButton _btnKeepNewest = null!;
    private ModernButton _btnCheckAll = null!;
    private ModernButton _btnUncheckAll = null!;
    private Label _lblStats = null!;
    private ModernButton _btnDelete = null!;
    private Panel _progressRow = null!;
    private Panel _actionsRow = null!;

    private int Px(float value) => (int)Math.Round(value * _scale);

    public MainForm()
    {
        Text = "OneCopy 重复文件清理工具";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Background;
        Font = Theme.UI;
        AutoScaleMode = AutoScaleMode.None; // 所有尺寸由 Px() 手动按 DPI 缩放
        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Theme.AppIcon;

        _ = Handle; // 提前创建句柄，取得当前显示器的真实 DPI
        _scale = DeviceDpi / 96f;
        ApplyWindowMetrics();

        SuspendLayout();
        BuildUI();
        ResumeLayout(performLayout: true);

        BindFormEvents();
    }

    // ===================== 无边框窗口 =====================

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX
            // 保留系统提供的可调整大小边框、阴影与贴边分屏，标题栏由我们自绘（通过 WM_NCCALCSIZE 去掉）。
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
                // 最大化时系统会让窗口向屏幕外延伸一圈可调整边框，这里把客户区对齐到工作区
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
            if ((int)m.Result == NativeMethods.HTCLIENT
                && WindowState == FormWindowState.Normal)
            {
                // 客户区铺满窗口后系统不再提供缩放边框，这里自行判断边缘
                int lParam = m.LParam.ToInt32();
                var screenPoint = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
                Point client = PointToClient(screenPoint);
                int border = Math.Max(6, Px(5));
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
            }
            return;
        }

        base.WndProc(ref m);
    }

    private void ApplyWindowMetrics()
    {
        MinimumSize = new Size(Px(DesignMinWidth), Px(DesignMinHeight));
        Size = new Size(Px(DesignWidth), Px(DesignHeight));
    }

    private void UpdateMaximizeState() =>
        _titleBar.SetMaximized(WindowState == FormWindowState.Maximized);

    // ===================== 界面 =====================

    [System.Diagnostics.CodeAnalysis.MemberNotNull(
        nameof(_titleBar), nameof(_btnSelectFolder), nameof(_pathBox), nameof(_list),
        nameof(_progress), nameof(_lblProgress), nameof(_btnCancel), nameof(_btnKeepFirst),
        nameof(_btnKeepNewest), nameof(_btnCheckAll), nameof(_btnUncheckAll),
        nameof(_lblStats), nameof(_btnDelete), nameof(_progressRow), nameof(_actionsRow))]
    private void BuildUI()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(TitleBarHeight)));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        _titleBar = new TitleBar(this) { Dock = DockStyle.Fill };
        _titleBar.ThemeToggleRequested += (_, _) => ToggleTheme();
        shell.Controls.Add(_titleBar, 0, 0);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(Px(24), Px(16), Px(24), Px(14)),
        };
        shell.Controls.Add(body, 0, 1);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(54)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(92)));
        body.Controls.Add(root);

        // ===== 顶部工具栏 =====
        var toolbar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        _btnSelectFolder = new ModernButton
        {
            Text = "选择文件夹",
            Kind = ButtonKind.Primary,
            Size = new Size(Px(124), Px(40)),
            Location = new Point(0, Px(4)),
        };
        _pathBox = new PathBox
        {
            Location = new Point(Px(136), Px(4)),
            Size = new Size(Px(400), Px(40)),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
        };
        toolbar.Controls.Add(_btnSelectFolder);
        toolbar.Controls.Add(_pathBox);
        toolbar.Resize += (_, _) => _pathBox.Width = Math.Max(0, toolbar.Width - _pathBox.Left);
        root.Controls.Add(toolbar, 0, 0);

        // ===== 列表卡片 =====
        var card = new CardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            ShadowSpread = 9,
            Padding = new Padding(Px(10), Px(6), Px(10), Px(10)),
        };
        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface,
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(32)));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cardLayout.Controls.Add(new ListHeaderPanel { Dock = DockStyle.Fill }, 0, 0);
        _list = new DuplicateListView { Dock = DockStyle.Fill };
        cardLayout.Controls.Add(_list, 0, 1);
        card.Controls.Add(cardLayout);
        root.Controls.Add(card, 0, 1);

        // ===== 底部操作区 =====
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
        };
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(38)));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(bottom, 0, 2);

        // 进度行
        _progressRow = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        _lblProgress = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.TextSecondary,
            Font = Theme.Small,
            Text = "就绪",
            BackColor = Theme.Background,
        };
        _progress = new ModernProgressBar();
        _btnCancel = new ModernButton
        {
            Text = "取消扫描",
            Kind = ButtonKind.Secondary,
            Size = new Size(Px(96), Px(32)),
            Visible = false,
        };
        _progressRow.Controls.Add(_lblProgress);
        _progressRow.Controls.Add(_progress);
        _progressRow.Controls.Add(_btnCancel);
        _progressRow.Resize += (_, _) => LayoutProgressRow();
        bottom.Controls.Add(_progressRow, 0, 0);

        // 操作行
        _actionsRow = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var quick = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = Px(660),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Padding = new Padding(0, Px(12), 0, 0),
        };
        _btnKeepFirst = CreateQuickButton("保留第一个", 116);
        _btnKeepNewest = CreateQuickButton("保留最新文件", 128);
        _btnCheckAll = CreateQuickButton("全选", 76);
        _btnUncheckAll = CreateQuickButton("取消全选", 96);
        quick.Controls.Add(_btnKeepFirst);
        quick.Controls.Add(_btnKeepNewest);
        quick.Controls.Add(CreateSeparator());
        quick.Controls.Add(_btnCheckAll);
        quick.Controls.Add(_btnUncheckAll);

        _lblStats = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Theme.TextMuted,
            Font = Theme.Small,
            Text = "尚未选择文件",
            BackColor = Theme.Background,
        };
        _btnDelete = new ModernButton
        {
            Text = "删除所选文件",
            Kind = ButtonKind.Danger,
            Size = new Size(Px(150), Px(40)),
            Enabled = false,
        };
        _actionsRow.Controls.Add(quick);
        _actionsRow.Controls.Add(_lblStats);
        _actionsRow.Controls.Add(_btnDelete);
        _actionsRow.Resize += (_, _) => LayoutActionsRow();
        bottom.Controls.Add(_actionsRow, 0, 1);

        UpdateMaximizeState();
        BindControlEvents();
    }

    private ModernButton CreateQuickButton(string text, int width) => new()
    {
        Text = text,
        Kind = ButtonKind.Secondary,
        Size = new Size(Px(width), Px(32)),
        Margin = new Padding(0, 0, Px(8), 0),
    };

    private Control CreateSeparator() => new Panel
    {
        Size = new Size(Px(1), Px(16)),
        BackColor = Theme.Border,
        Margin = new Padding(Px(4), Px(8), Px(12), 0),
    };

    private void LayoutProgressRow()
    {
        int width = _progressRow.ClientSize.Width;
        int height = _progressRow.ClientSize.Height;
        int labelWidth = Math.Min(Px(380), Math.Max(Px(160), width / 3));
        int cancelWidth = _btnCancel.Visible ? _btnCancel.Width : 0;
        int progressLeft = labelWidth + Px(14);
        int progressRight = width - (cancelWidth > 0 ? cancelWidth + Px(14) : 0);

        _lblProgress.Bounds = new Rectangle(0, 0, labelWidth, height);
        _progress.Bounds = new Rectangle(progressLeft, 0, Math.Max(0, progressRight - progressLeft), height);
        if (_btnCancel.Visible)
        {
            _btnCancel.Bounds = new Rectangle(
                width - cancelWidth,
                (height - _btnCancel.Height) / 2,
                cancelWidth,
                _btnCancel.Height);
        }
    }

    private void LayoutActionsRow()
    {
        int width = _actionsRow.ClientSize.Width;
        int height = _actionsRow.ClientSize.Height;
        int statsWidth = Px(330);
        int gap = Px(16);

        _btnDelete.Bounds = new Rectangle(
            width - _btnDelete.Width,
            (height - _btnDelete.Height) / 2,
            _btnDelete.Width,
            _btnDelete.Height);

        int statsLeft = Math.Max(0, _btnDelete.Left - gap - statsWidth);
        _lblStats.Bounds = new Rectangle(statsLeft, (height - Px(24)) / 2, statsWidth, Px(24));
    }

    private void BindFormEvents()
    {
        KeyDown += OnFormKeyDown;
        FormClosing += OnFormClosing;
        DpiChanged += OnDpiChanged;
        Resize += (_, _) => UpdateMaximizeState();
    }

    private void BindControlEvents()
    {
        _btnSelectFolder.Click += BtnSelectFolder_Click;
        _btnCancel.Click += (_, _) =>
        {
            if (!_isScanning) return;
            _scanCts?.Cancel();
            _btnCancel.Enabled = false;
            _lblProgress.Text = "正在取消…";
        };
        _btnKeepFirst.Click += (_, _) => QuickKeep(KeepMode.First);
        _btnKeepNewest.Click += (_, _) => QuickKeep(KeepMode.Newest);
        _btnCheckAll.Click += (_, _) => QuickKeep(KeepMode.SelectAll);
        _btnUncheckAll.Click += (_, _) => QuickKeep(KeepMode.DeselectAll);
        _btnDelete.Click += BtnDelete_Click;
        _list.CheckedChanged += (_, _) => UpdateStats();
    }

    // ===================== 主题与 DPI 变化：重建界面 =====================

    private void ToggleTheme()
    {
        Theme.SetDarkMode(!Theme.DarkMode);
        RebuildUI();
    }

    private void OnDpiChanged(object? sender, DpiChangedEventArgs e)
    {
        _scale = e.DeviceDpiNew / 96f;
        ApplyWindowMetrics();
        RebuildUI();
    }

    private void RebuildUI()
    {
        // 先保存当前界面状态
        string folder = _pathBox?.Value ?? string.Empty;
        string progressText = _lblProgress?.Text ?? "就绪";
        int progressValue = _progress?.Value ?? 0;
        int progressMaximum = _progress?.Maximum ?? 100;
        bool indeterminate = _progress?.Indeterminate ?? false;
        bool scanning = _isScanning;
        bool summarizing = _summarizing;
        bool maximized = WindowState == FormWindowState.Maximized;

        SuspendLayout();
        for (int i = Controls.Count - 1; i >= 0; i--)
        {
            Control control = Controls[i];
            Controls.RemoveAt(i);
            control.Dispose();
        }
        BuildUI();
        ResumeLayout(performLayout: true);

        // 恢复状态
        _isScanning = scanning;
        _summarizing = summarizing;
        if (!string.IsNullOrEmpty(folder)) _pathBox.Value = folder;
        _list.SetGroups(_allGroups);
        if (_allGroups.Count == 0 && !scanning)
        {
            _list.SetEmptyState("还没有重复文件", "选择一个文件夹，开始查找内容完全相同的文件");
        }
        _progress.Maximum = progressMaximum;
        _progress.Value = progressValue;
        _progress.Indeterminate = indeterminate;
        _lblProgress.Text = progressText;
        _btnCancel.Visible = scanning;
        _btnCancel.Enabled = scanning;
        _btnSelectFolder.Enabled = !scanning && !summarizing;
        UpdateActionAvailability();
        LayoutProgressRow();
        LayoutActionsRow();
        _titleBar.SetMaximized(maximized);
        WindowChrome.Apply(this);
    }

    // ===================== 扫描 =====================

    private async void BtnSelectFolder_Click(object? sender, EventArgs e)
    {
        if (_isScanning || _summarizing) return;

        using var dlg = new FolderBrowserDialog
        {
            Description = "选择要扫描的文件夹：",
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string path = dlg.SelectedPath;
        _pathBox.Value = path;

        _summarizing = true;
        _btnSelectFolder.Enabled = false;
        _lblProgress.Text = "正在统计文件…";
        _progress.Indeterminate = true;
        try
        {
            FileSummary summary = await Task.Run(() => FileScanner.GetFileSummary(path));
            _progress.Indeterminate = false;
            _progress.Maximum = 100;
            _progress.Value = 0;
            _lblProgress.Text = "就绪";

            if (summary.TotalFiles == 0)
            {
                MessageBox.Show(this, "这个文件夹里没有找到文件。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!Confirm(summary, path)) return;
            await DoScanAsync(path);
        }
        catch (Exception ex)
        {
            _progress.Indeterminate = false;
            _lblProgress.Text = "就绪";
            MessageBox.Show(this, $"读取文件夹失败：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _summarizing = false;
            if (!_isScanning) _btnSelectFolder.Enabled = true;
            if (_closing) BeginInvoke(new Action(Close));
        }
    }

    private bool Confirm(FileSummary summary, string folder)
    {
        using Form dialog = CreateScanConfirmDialog(summary, folder);
        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    internal Form CreateScanConfirmDialog(FileSummary summary, string folder)
    {
        var dialog = new Form
        {
            Text = "扫描确认",
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
            BackColor = Theme.Border,
            Font = Theme.UI,
            AutoScaleMode = AutoScaleMode.None,
            ClientSize = new Size(Px(560), Px(478)),
            Padding = new Padding(1),
            Icon = Theme.AppIcon,
        };
        dialog.HandleCreated += (_, _) => WindowChrome.Apply(dialog);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(44)));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        dialog.Controls.Add(shell);

        var titleBar = new TitleBar(dialog)
        {
            Dock = DockStyle.Fill,
            ShowMinimize = false,
            ShowMaximize = false,
            ShowThemeToggle = false,
            ShowVersion = false,
        };
        shell.Controls.Add(titleBar, 0, 0);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(Px(22), Px(14), Px(22), Px(16)),
        };
        shell.Controls.Add(body, 0, 1);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(40)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(64)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(52)));
        body.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "确认扫描范围",
            Font = Theme.Title,
            ForeColor = Theme.TextPrimary,
            BackColor = Theme.Background,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = Theme.Small,
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Background,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"文件夹：{folder}\n共 {summary.TotalFiles:n0} 个文件 · 总大小 {summary.TotalSizeDisplay}（按扩展名统计如下）",
        }, 0, 1);

        var card = new CardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            ShadowSpread = 5,
            Padding = new Padding(Px(8)),
        };
        var statsView = new ExtensionStatsView { Dock = DockStyle.Fill };
        statsView.SetItems(summary.Extensions);
        card.Controls.Add(statsView);
        root.Controls.Add(card, 0, 2);

        var buttons = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var btnCancel = new ModernButton
        {
            Text = "取消",
            Kind = ButtonKind.Secondary,
            Size = new Size(Px(96), Px(38)),
            DialogResult = DialogResult.Cancel,
        };
        var btnStart = new ModernButton
        {
            Text = "开始扫描",
            Kind = ButtonKind.Primary,
            Size = new Size(Px(124), Px(38)),
            DialogResult = DialogResult.OK,
        };
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnStart);
        buttons.Resize += (_, _) =>
        {
            int top = (buttons.Height - btnStart.Height) / 2;
            btnStart.Location = new Point(buttons.Width - btnStart.Width, top);
            btnCancel.Location = new Point(btnStart.Left - btnCancel.Width - Px(10), top);
        };
        root.Controls.Add(buttons, 0, 3);

        dialog.AcceptButton = btnStart;
        dialog.CancelButton = btnCancel;
        return dialog;
    }

    private async Task DoScanAsync(string path)
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        CancellationToken token = _scanCts.Token;

        _allGroups.Clear();
        _list.SetGroups(Array.Empty<DuplicateGroup>());
        _list.SetEmptyState("正在扫描…", "扫描完成后，重复文件会显示在这里");
        BeginScanningUI();

        try
        {
            var progress = new Progress<ScanProgress>(OnScanProgress);
            _allGroups = await new FileScanner().ScanAsync(path, progress, token);

            _list.SetGroups(_allGroups);
            if (_allGroups.Count == 0)
            {
                _list.SetEmptyState("没有发现重复文件", "所选文件夹内没有内容完全相同的文件");
            }

            _progress.Indeterminate = false;
            _progress.Maximum = 100;
            _progress.Value = 100;
            _lblProgress.Text = _allGroups.Count > 0
                ? $"完成 · {_allGroups.Count} 组重复 · {_allGroups.Sum(g => g.FileCount)} 个文件"
                : "完成 · 没有发现重复文件";
        }
        catch (OperationCanceledException)
        {
            _list.SetEmptyState("扫描已取消", "重新选择文件夹即可再次扫描");
            _progress.Indeterminate = false;
            _progress.Value = 0;
            _lblProgress.Text = "已取消";
        }
        catch (Exception ex)
        {
            _list.SetEmptyState("扫描失败", "请检查文件夹权限后重试");
            _progress.Indeterminate = false;
            _progress.Value = 0;
            _lblProgress.Text = "扫描失败";
            MessageBox.Show(this, $"扫描出错：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            EndScanningUI();
        }
    }

    private void OnScanProgress(ScanProgress info)
    {
        switch (info.Phase)
        {
            case 0:
                _progress.Indeterminate = true;
                _lblProgress.Text = info.ProcessedFiles > 0
                    ? $"正在枚举文件… 已找到 {info.ProcessedFiles:n0} 个"
                    : "正在枚举文件…";
                break;

            case 1:
                _progress.Indeterminate = false;
                _progress.Maximum = Math.Max(1, info.TotalFiles);
                _progress.Value = info.ProcessedFiles;
                int percent = info.TotalFiles > 0
                    ? (int)(info.ProcessedFiles * 100L / info.TotalFiles)
                    : 100;
                _lblProgress.Text = $"正在计算 SHA-256 · {info.ProcessedFiles:n0} / {info.TotalFiles:n0} · {percent}%";
                break;

            default:
                _lblProgress.Text = "正在整理结果…";
                break;
        }
    }

    private void BeginScanningUI()
    {
        _isScanning = true;
        _btnSelectFolder.Enabled = false;
        _btnCancel.Visible = true;
        _btnCancel.Enabled = true;
        _progress.Indeterminate = true;
        _progress.Maximum = 100;
        _progress.Value = 0;
        _lblProgress.Text = "开始扫描…";
        UpdateActionAvailability();
        LayoutProgressRow();
    }

    private void EndScanningUI()
    {
        _isScanning = false;
        _btnCancel.Visible = false;
        _btnCancel.Enabled = false;
        _btnSelectFolder.Enabled = true;
        UpdateActionAvailability();
        LayoutProgressRow();
        if (_closing) BeginInvoke(new Action(Close));
    }

    private void UpdateActionAvailability()
    {
        bool ready = !_isScanning && _allGroups.Count > 0;
        _btnKeepFirst.Enabled = ready;
        _btnKeepNewest.Enabled = ready;
        _btnCheckAll.Enabled = ready;
        _btnUncheckAll.Enabled = ready;
        UpdateStats();
    }

    // ===================== 勾选与统计 =====================

    private void QuickKeep(KeepMode mode)
    {
        if (_allGroups.Count == 0) return;

        foreach (DuplicateGroup group in _allGroups)
        {
            switch (mode)
            {
                case KeepMode.SelectAll:
                    group.Files.ForEach(f => f.IsChecked = true);
                    continue;
                case KeepMode.DeselectAll:
                    group.Files.ForEach(f => f.IsChecked = false);
                    continue;
                case KeepMode.First:
                    MarkKeepOnly(group, group.Files[0]);
                    break;
                case KeepMode.Newest:
                    MarkKeepOnly(group, group.Files
                        .OrderByDescending(f => f.LastWriteTime)
                        .ThenBy(f => f.FilePath)
                        .First());
                    break;
            }
        }

        _list.RefreshState();
        UpdateStats();
    }

    private static void MarkKeepOnly(DuplicateGroup group, FileItem keep)
    {
        foreach (FileItem file in group.Files)
        {
            file.IsChecked = !ReferenceEquals(file, keep);
        }
    }

    private void UpdateStats()
    {
        int count = _list.CheckedFileCount;
        long bytes = _list.CheckedFileBytes;
        bool wholeGroupSelected = _allGroups.Any(g => g.Files.Count > 0 && g.Files.All(f => f.IsChecked));

        if (count == 0)
        {
            _lblStats.Text = "尚未选择文件";
            _lblStats.ForeColor = Theme.TextMuted;
        }
        else if (wholeGroupSelected)
        {
            _lblStats.Text = $"已选 {count:n0} 个文件 · 含整组全选";
            _lblStats.ForeColor = Theme.Danger;
        }
        else
        {
            _lblStats.Text = $"已选 {count:n0} 个文件 · 可释放 {FileItem.FormatSize(bytes)}";
            _lblStats.ForeColor = Theme.TextSecondary;
        }

        _btnDelete.Enabled = count > 0 && !_isScanning && !_summarizing;
    }

    // ===================== 删除 =====================

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        List<FileItem> files = _allGroups.SelectMany(g => g.Files).Where(f => f.IsChecked).ToList();
        if (files.Count == 0) return;

        long bytes = files.Sum(f => f.FileSize);
        bool wholeGroupSelected = _allGroups.Any(g => g.Files.Count > 0 && g.Files.All(f => f.IsChecked));

        string message = $"将永久删除 {files.Count:n0} 个文件（{FileItem.FormatSize(bytes)}）。\n\n此操作不可恢复，确定继续吗？";
        if (wholeGroupSelected)
        {
            message += "\n\n注意：存在某个重复组的所有副本都被选中，删除后这些内容将不再保留任何一份。";
        }

        if (MessageBox.Show(this, message, "确认删除", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        int deleted = 0;
        var failures = new List<string>();
        foreach (FileItem file in files)
        {
            try
            {
                File.Delete(file.FilePath);
                deleted++;
            }
            catch (Exception ex)
            {
                failures.Add($"{file.FileName}：{ex.Message}");
            }
        }

        foreach (DuplicateGroup group in _allGroups)
        {
            group.Files.RemoveAll(f => f.IsChecked && !File.Exists(f.FilePath));
        }
        _allGroups.RemoveAll(g => g.Files.Count < 2);

        _list.SetGroups(_allGroups);
        if (_allGroups.Count == 0)
        {
            _list.SetEmptyState("没有重复文件了", "所选文件夹中的重复项已全部处理");
        }
        UpdateActionAvailability();

        _progress.Indeterminate = false;
        _progress.Maximum = 100;
        _progress.Value = 100;
        _lblProgress.Text = failures.Count == 0
            ? $"已删除 {deleted:n0} 个文件"
            : $"已删除 {deleted:n0} 个文件 · {failures.Count:n0} 个失败";

        if (failures.Count > 0)
        {
            string detail = string.Join("\n", failures.Take(8));
            if (failures.Count > 8) detail += $"\n… 还有 {failures.Count - 8} 个";
            MessageBox.Show(this, $"部分文件删除失败（可能被占用或权限不足）：\n\n{detail}",
                "删除结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ===================== 键盘与关闭 =====================

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.A && !_isScanning && _allGroups.Count > 0)
        {
            QuickKeep(KeepMode.SelectAll);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete && _btnDelete.Enabled)
        {
            BtnDelete_Click(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape && _isScanning)
        {
            _scanCts?.Cancel();
            e.Handled = true;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isScanning && !_summarizing) return;
        _closing = true;
        _scanCts?.Cancel();
        e.Cancel = true;
        if (!_isScanning) BeginInvoke(new Action(Close));
    }

    // ===================== 预览 / 自检辅助（Debug） =====================

    internal void ApplyPreviewData(string folder, List<DuplicateGroup> groups)
    {
        _allGroups = groups;
        _pathBox.Value = folder;
        _list.SetGroups(groups);
        _progress.Indeterminate = false;

        if (groups.Count == 0)
        {
            _list.SetEmptyState("还没有重复文件", "选择一个文件夹，开始查找内容完全相同的文件");
            _progress.Maximum = 100;
            _progress.Value = 0;
            _lblProgress.Text = "就绪";
            UpdateActionAvailability();
            return;
        }

        _progress.Maximum = 100;
        _progress.Value = 100;
        _lblProgress.Text = $"完成 · {groups.Count} 组重复 · {groups.Sum(g => g.FileCount)} 个文件";
        UpdateActionAvailability();
    }

    internal void ToggleThemeForTest() => ToggleTheme();
}
