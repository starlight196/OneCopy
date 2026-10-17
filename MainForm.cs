// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

namespace OneCopy;

public sealed class MainForm : ChromeForm
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
    private FileScanner? _scanner;
    private bool _isScanning;
    private bool _isPaused;
    private bool _summarizing;
    private bool _closing;
    private float _scale = 1f;

    private readonly AppSettings _settings;
    private HashSet<string> _excludedExtensions;
    private readonly System.Windows.Forms.Timer _appearanceTimer;

    private TitleBar _titleBar = null!;
    private ModernButton _btnSelectFolder = null!;
    private PathBox _pathBox = null!;
    private DuplicateListView _list = null!;
    private ModernProgressBar _progress = null!;
    private Label _lblProgress = null!;
    private ModernButton _btnPause = null!;
    private ModernButton _btnStop = null!;
    private ModernButton _btnKeepFirst = null!;
    private ModernButton _btnKeepNewest = null!;
    private ModernButton _btnCheckAll = null!;
    private ModernButton _btnUncheckAll = null!;
    private Label _lblStats = null!;
    private ModernButton _btnDelete = null!;
    private Panel _progressRow = null!;
    private Panel _actionsRow = null!;

    private int Px(float value) => (int)Math.Round(value * _scale);

    public MainForm() : this(AppSettings.Load())
    {
    }

    internal MainForm(AppSettings settings)
    {
        _settings = settings;
        _excludedExtensions = settings.BuildExcludedSet();

        Text = "OneCopy 重复文件清理工具";
        BackColor = Theme.Background;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;

        _ = Handle; // 提前创建句柄，取得当前显示器的真实 DPI
        _scale = DeviceDpi / 96f;
        ApplyWindowMetrics();

        SuspendLayout();
        BuildUI();
        ResumeLayout(performLayout: true);

        BindFormEvents();

        // 跟随系统 / 自定义时间模式下定时检查是否需要换肤
        _appearanceTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _appearanceTimer.Tick += (_, _) => RefreshAppearanceIfNeeded();
        _appearanceTimer.Start();
    }

    // ===================== 无边框窗口 =====================

    /// <summary>主窗口允许拖动边缘缩放；圆角 / 阴影 / 拖动由 ChromeForm 统一提供。</summary>
    protected override bool AllowResize => true;

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
        nameof(_progress), nameof(_lblProgress), nameof(_btnPause), nameof(_btnStop), nameof(_btnKeepFirst),
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
        _titleBar.SettingsRequested += (_, _) => OpenSettings();
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
            Text = ReadyText,
            BackColor = Theme.Background,
        };
        _progress = new ModernProgressBar();
        _btnPause = new ModernButton
        {
            Text = "暂停",
            Kind = ButtonKind.Secondary,
            Size = new Size(Px(88), Px(32)),
            Visible = false,
        };
        _btnStop = new ModernButton
        {
            Text = "停止扫描",
            Kind = ButtonKind.Secondary,
            Size = new Size(Px(104), Px(32)),
            Visible = false,
        };
        _progressRow.Controls.Add(_lblProgress);
        _progressRow.Controls.Add(_progress);
        _progressRow.Controls.Add(_btnPause);
        _progressRow.Controls.Add(_btnStop);
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

        int stopWidth = _btnStop.Visible ? _btnStop.Width : 0;
        int pauseWidth = _btnPause.Visible ? _btnPause.Width : 0;
        int buttonsWidth = 0;
        if (stopWidth > 0) buttonsWidth += stopWidth + Px(14);
        if (pauseWidth > 0) buttonsWidth += pauseWidth + Px(8);
        if (pauseWidth > 0 && stopWidth == 0) buttonsWidth -= Px(8);

        int progressLeft = labelWidth + Px(14);
        int progressRight = width - buttonsWidth;

        _lblProgress.Bounds = new Rectangle(0, 0, labelWidth, height);
        _progress.Bounds = new Rectangle(progressLeft, 0, Math.Max(0, progressRight - progressLeft), height);

        if (stopWidth > 0)
        {
            _btnStop.Bounds = new Rectangle(
                width - stopWidth,
                (height - _btnStop.Height) / 2,
                stopWidth,
                _btnStop.Height);
        }

        if (pauseWidth > 0)
        {
            int pauseRight = stopWidth > 0 ? _btnStop.Left - Px(8) : width;
            _btnPause.Bounds = new Rectangle(
                pauseRight - pauseWidth,
                (height - _btnPause.Height) / 2,
                pauseWidth,
                _btnPause.Height);
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
        _btnPause.Click += (_, _) => TogglePause();
        _btnStop.Click += (_, _) =>
        {
            if (!_isScanning) return;
            _scanCts?.Cancel();
            _btnPause.Enabled = false;
            _btnStop.Enabled = false;
            _lblProgress.Text = "正在停止扫描…";
        };
        _btnKeepFirst.Click += (_, _) => QuickKeep(KeepMode.First);
        _btnKeepNewest.Click += (_, _) => QuickKeep(KeepMode.Newest);
        _btnCheckAll.Click += (_, _) => QuickKeep(KeepMode.SelectAll);
        _btnUncheckAll.Click += (_, _) => QuickKeep(KeepMode.DeselectAll);
        _btnDelete.Click += BtnDelete_Click;
        _list.CheckedChanged += (_, _) => UpdateStats();
    }

    // ===================== 主题、设置与 DPI 变化 =====================

    private string ReadyText => _excludedExtensions.Count > 0
        ? $"就绪 · 已排除 {_excludedExtensions.Count} 种扩展名"
        : "就绪";

    private void OpenSettings()
    {
        // 设置窗口打开期间暂停自动换肤，避免背景窗口在对话框后面重建
        _appearanceTimer.Stop();
        try
        {
            using var dialog = new SettingsForm(_settings);

            // 外观卡片 / 时间修改后立即预览主题（对话框内部已经换肤，这里同步主窗口）
            dialog.PreviewThemeRequested += (_, _) => RebuildUI();

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                // 取消：恢复打开设置前的主题
                bool originalDark = _settings.ResolveDark(DateTime.Now);
                if (originalDark != Theme.DarkMode)
                {
                    Theme.SetDarkMode(originalDark);
                    RebuildUI();
                }
                return;
            }

            ApplySettings(dialog.Result);
        }
        finally
        {
            _appearanceTimer.Start();
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        _settings.CopyFrom(settings);
        _settings.Save();
        _excludedExtensions = _settings.BuildExcludedSet();
        Theme.SetDarkMode(_settings.ResolveDark(DateTime.Now));
        RebuildUI();
        _lblProgress.Text = ReadyText;
    }

    /// <summary>跟随系统 / 自定义时间模式下，时钟跨越时段或系统主题变化时自动换肤。</summary>
    private void RefreshAppearanceIfNeeded()
    {
        if (_closing || IsDisposed || !IsHandleCreated) return;
        if (_settings.ThemeMode is ThemeMode.Light or ThemeMode.Dark) return;

        bool dark = _settings.ResolveDark(DateTime.Now);
        if (dark == Theme.DarkMode) return;

        Theme.SetDarkMode(dark);
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
        bool paused = _isPaused;
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
        _isPaused = paused;
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
        _lblProgress.Text = paused && scanning ? PausedText : progressText;
        _btnPause.Text = paused ? "继续" : "暂停";
        _btnPause.Visible = scanning;
        _btnPause.Enabled = scanning;
        _btnStop.Visible = scanning;
        _btnStop.Enabled = scanning;
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
            HashSet<string> excluded = _excludedExtensions;
            FileSummary summary = await Task.Run(() => FileScanner.GetFileSummary(path, default, excluded));
            _progress.Indeterminate = false;
            _progress.Maximum = 100;
            _progress.Value = 0;
            _lblProgress.Text = ReadyText;

            if (summary.TotalFiles == 0)
            {
                string message = excluded.Count > 0
                    ? "按当前全局排除规则，这个文件夹里没有需要扫描的文件。可以在设置中调整排除的扩展名。"
                    : "这个文件夹里没有找到文件。";
                MessageBox.Show(this, message, "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 大文件夹提醒（可在设置 → 扫描器中调整阈值或关闭）
            if (_settings.WarnLargeFolder)
            {
                long thresholdBytes = (long)_settings.LargeFolderWarningGb * 1024 * 1024 * 1024;
                if (summary.TotalSize > thresholdBytes)
                {
                    DialogResult go = MessageBox.Show(
                        this,
                        $"该文件夹共 {summary.TotalSizeDisplay}（{summary.TotalFiles:n0} 个文件），" +
                        $"超过提醒阈值 {_settings.LargeFolderWarningGb} GB。\n\n扫描可能需要较长时间，是否继续？",
                        "文件夹较大",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1);
                    if (go != DialogResult.Yes) return;
                }
            }

            if (!Confirm(summary, path)) return;
            await DoScanAsync(path);
        }
        catch (Exception ex)
        {
            _progress.Indeterminate = false;
            _lblProgress.Text = ReadyText;
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
        var dialog = new ChromeDialog
        {
            Text = "扫描确认",
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
            BackColor = Theme.Background,
            Size = new Size(Px(560), Px(498)),
            Icon = Theme.AppIcon,
        };

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
            ShowSettings = false,
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(84)));
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

        string scanInfo =
            $"算法 {HashAlgorithmInfo.DisplayName(_settings.HashAlgorithm)} · " +
            $"并行 {(_settings.AutoParallelism ? Environment.ProcessorCount : _settings.MaxParallelism)}";
        if (_excludedExtensions.Count > 0)
        {
            scanInfo += $" · 已排除 {_excludedExtensions.Count} 种扩展名";
        }
        scanInfo += "（可在设置 → 扫描器中修改）";

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = Theme.Small,
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Background,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"文件夹：{folder}\n共 {summary.TotalFiles:n0} 个文件 · 总大小 {summary.TotalSizeDisplay}（按扩展名统计如下）\n{scanInfo}",
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
            _scanner = new FileScanner
            {
                ExcludedExtensions = _excludedExtensions,
                HashAlgorithm = _settings.HashAlgorithm,
                MaxDegreeOfParallelism = _settings.AutoParallelism ? 0 : _settings.MaxParallelism,
            };
            _allGroups = await _scanner.ScanAsync(path, progress, token);

            _list.SetGroups(_allGroups);
            if (_allGroups.Count == 0)
            {
                _list.SetEmptyState(
                    "没有发现重复文件",
                    _excludedExtensions.Count > 0
                        ? "所选文件夹内没有重复文件（已按设置排除部分扩展名）"
                        : "所选文件夹内没有内容完全相同的文件");
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

    private void TogglePause()
    {
        if (!_isScanning || _scanner is null) return;

        if (_isPaused)
        {
            _scanner.Resume();
            _isPaused = false;
            _btnPause.Text = "暂停";
            _lblProgress.Text = "继续扫描…";
        }
        else
        {
            _scanner.Pause();
            _isPaused = true;
            _btnPause.Text = "继续";
            _lblProgress.Text = PausedText;
        }
    }

    private static string PausedText => "已暂停 · 点击「继续」恢复扫描";

    private void OnScanProgress(ScanProgress info)
    {
        switch (info.Phase)
        {
            case 0:
                _progress.Indeterminate = true;
                if (_isPaused)
                {
                    _lblProgress.Text = PausedText;
                    break;
                }
                _lblProgress.Text = info.ProcessedFiles > 0
                    ? $"正在枚举文件… 已找到 {info.ProcessedFiles:n0} 个"
                    : "正在枚举文件…";
                break;

            case 1:
                _progress.Indeterminate = false;
                _progress.Maximum = Math.Max(1, info.TotalFiles);
                _progress.Value = info.ProcessedFiles;
                if (_isPaused)
                {
                    _lblProgress.Text = PausedText;
                    break;
                }
                int percent = info.TotalFiles > 0
                    ? (int)(info.ProcessedFiles * 100L / info.TotalFiles)
                    : 100;
                _lblProgress.Text = $"正在计算 {HashAlgorithmInfo.DisplayName(_settings.HashAlgorithm)} · {info.ProcessedFiles:n0} / {info.TotalFiles:n0} · {percent}%";
                break;

            default:
                if (!_isPaused) _lblProgress.Text = "正在整理结果…";
                break;
        }
    }

    private void BeginScanningUI()
    {
        _isScanning = true;
        _isPaused = false;
        _btnSelectFolder.Enabled = false;
        _btnPause.Text = "暂停";
        _btnPause.Visible = true;
        _btnPause.Enabled = true;
        _btnStop.Visible = true;
        _btnStop.Enabled = true;
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
        _isPaused = false;
        _scanner = null;
        _btnPause.Text = "暂停";
        _btnPause.Visible = false;
        _btnPause.Enabled = false;
        _btnStop.Visible = false;
        _btnStop.Enabled = false;
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
        if (e.Control && e.KeyCode == Keys.Oemcomma)
        {
            OpenSettings();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.A && !_isScanning && _allGroups.Count > 0)
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
            _btnPause.Enabled = false;
            _btnStop.Enabled = false;
            _lblProgress.Text = "正在停止扫描…";
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _appearanceTimer?.Stop();
            _appearanceTimer?.Dispose();
            _scanCts?.Cancel();
            _scanCts?.Dispose();
        }
        base.Dispose(disposing);
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
            _lblProgress.Text = ReadyText;
            UpdateActionAvailability();
            return;
        }

        _progress.Maximum = 100;
        _progress.Value = 100;
        _lblProgress.Text = $"完成 · {groups.Count} 组重复 · {groups.Sum(g => g.FileCount)} 个文件";
        UpdateActionAvailability();
    }

    internal SettingsForm CreateSettingsDialog() => new(_settings);

    internal void ApplySettingsForTest(AppSettings settings) => ApplySettings(settings);

    internal HashSet<string> ExcludedExtensionsForTest => _excludedExtensions;

    internal HashAlgorithmKind HashAlgorithmForTest => _settings.HashAlgorithm;

    internal bool WarnLargeFolderForTest => _settings.WarnLargeFolder;

    internal int LargeFolderWarningGbForTest => _settings.LargeFolderWarningGb;

    /// <summary>预览 / 自检用：把界面切到"正在扫描"状态，检查暂停与停止按钮。</summary>
    internal void ApplyScanningPreviewForTest()
    {
        _isScanning = true;
        _isPaused = false;
        _btnSelectFolder.Enabled = false;
        _btnPause.Text = "暂停";
        _btnPause.Visible = true;
        _btnPause.Enabled = true;
        _btnStop.Visible = true;
        _btnStop.Enabled = true;
        _progress.Indeterminate = false;
        _progress.Maximum = 2048;
        _progress.Value = 1284;
        _lblProgress.Text = $"正在计算 {HashAlgorithmInfo.DisplayName(_settings.HashAlgorithm)} · 1,284 / 2,048 · 62%";
        UpdateActionAvailability();
        LayoutProgressRow();
    }

    /// <summary>预览 / 自检用：取消"正在扫描"状态。</summary>
    internal void ClearScanningPreviewForTest()
    {
        _isScanning = false;
        _isPaused = false;
        _btnPause.Visible = false;
        _btnPause.Enabled = false;
        _btnPause.Text = "暂停";
        _btnStop.Visible = false;
        _btnStop.Enabled = false;
        _progress.Indeterminate = false;
        _progress.Maximum = 100;
        _progress.Value = 100;
        _lblProgress.Text = "完成 · 6 组重复 · 12 个文件";
        LayoutProgressRow();
    }

    internal bool PauseButtonVisibleForTest => _btnPause.Visible;

    internal string PauseButtonTextForTest => _btnPause.Text;

    internal bool StopButtonVisibleForTest => _btnStop.Visible;

    internal string StopButtonTextForTest => _btnStop.Text;

    /// <summary>自检用：切换暂停状态（需要有正在运行的扫描器）。</summary>
    internal void TogglePauseForTest() => TogglePause();

    /// <summary>自检用：提供当前扫描器（测试暂停闸门使用）。</summary>
    internal void SetScannerForTest(FileScanner scanner) => _scanner = scanner;

    /// <summary>自检用：读取暂停状态。</summary>
    internal bool IsPausedForTest => _isPaused;
}
