// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace OneCopy;

/// <summary>设置窗口的页面。</summary>
internal enum SettingsPage
{
    Appearance,
    Scanner,
    Exclusions,
    Update,
    About,
}

/// <summary>
/// 设置窗口：左侧导航 + 右侧内容区。
/// 页面：外观、扫描器（哈希算法 / 大文件夹提醒 / 并行数）、全局排除扩展名、检查更新、关于。
/// </summary>
internal sealed class SettingsForm : ChromeForm
{
    private readonly AppSettings _result;
    private readonly List<NavItem> _navItems = new();
    private readonly Dictionary<SettingsPage, Panel> _pages = new();
    private readonly List<ThemePreviewCard> _themeCards = new();
    private readonly List<OptionChip> _algorithmChips = new();

    private AppLogo _logo = null!;
    private Label _appName = null!;
    private Label _appSubtitle = null!;
    private Label _sidebarVersion = null!;
    private SidebarPanel _sidebar = null!;
    private Panel _pageHost = null!;
    private Panel _bottomBar = null!;
    private Label _statusLabel = null!;
    private ModernButton _btnSave = null!;
    private ModernButton _btnCancel = null!;

    private TimeField _darkStart = null!;
    private TimeField _darkEnd = null!;
    private MultilineInput _extensions = null!;
    private Label _extensionsCount = null!;

    private HashAlgorithmKind _algorithm;
    private Label _algorithmDescription = null!;
    private ToggleSwitch _warnToggle = null!;
    private NumberField _warnGb = null!;
    private ToggleSwitch _autoParallelToggle = null!;
    private NumberField _parallelCount = null!;

    private Label _updateCurrent = null!;
    private Label _updateStatus = null!;
    private ModernButton _btnCheckUpdate = null!;
    private ModernButton _btnOpenReleases = null!;
    private string _releaseUrl = UpdateChecker.ReleasesUrl;

    private ThemeMode _mode;
    private SettingsPage _page = SettingsPage.Appearance;
    private bool _previewing;
    private bool _rebuilding;

    /// <summary>外观预览：主题已经切换后触发，宿主窗口据此同步换肤。</summary>
    public event EventHandler? PreviewThemeRequested;

    /// <summary>保存后的设置（仅在对话框返回 OK 时有效）。</summary>
    public AppSettings Result => _result;

    public SettingsForm(AppSettings settings)
    {
        _result = settings.Clone();
        _mode = _result.ThemeMode;
        _algorithm = _result.HashAlgorithm;

        Text = "设置";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Theme.Background;
        DialogResult = DialogResult.Cancel;

        _ = Handle; // 提前创建句柄，取得真实 DPI
        Size = new Size(Px(900), Px(660));

        SuspendLayout();
        BuildUI();
        ResumeLayout(performLayout: true);
    }

    private int Px(float value) => ScalePx(value);

    // ===================== 整体布局 =====================

    private void BuildUI()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(44)));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        var titleBar = new TitleBar(this)
        {
            Dock = DockStyle.Fill,
            ShowMinimize = false,
            ShowMaximize = false,
            ShowSettings = false,
            ShowVersion = false,
        };
        shell.Controls.Add(titleBar, 0, 0);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        shell.Controls.Add(body, 0, 1);

        // 左侧导航 + 右侧内容用两列布局，避免 Dock 顺序导致的相互覆盖
        var bodyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
        };
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(210)));
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(bodyLayout);

        _sidebar = new SidebarPanel { Dock = DockStyle.Fill };
        bodyLayout.Controls.Add(_sidebar, 0, 0);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(Px(24), Px(20), Px(24), Px(18)),
        };
        bodyLayout.Controls.Add(content, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(52)));
        content.Controls.Add(layout);

        _pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        layout.Controls.Add(_pageHost, 0, 0);

        // 所有页面一次性创建，切换时只改变可见性
        _pages[SettingsPage.Appearance] = BuildAppearancePage();
        _pages[SettingsPage.Scanner] = BuildScannerPage();
        _pages[SettingsPage.Exclusions] = BuildExclusionsPage();
        _pages[SettingsPage.Update] = BuildUpdatePage();
        _pages[SettingsPage.About] = BuildAboutPage();
        foreach (Panel page in _pages.Values)
        {
            page.Visible = false;
            _pageHost.Controls.Add(page);
        }

        _bottomBar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        layout.Controls.Add(_bottomBar, 0, 1);

        BuildSidebar();
        BuildBottomBar();

        SelectPage(SettingsPage.Appearance);
        SelectMode(_mode);
        Shown += (_, _) => _navItems[0].Focus();
    }

    private void BuildSidebar()
    {
        _logo = new AppLogo { Size = new Size(Px(34), Px(34)) };
        _appName = new Label
        {
            AutoSize = false,
            Text = "OneCopy",
            Font = Theme.TitleBarFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Theme.Surface,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _appSubtitle = new Label
        {
            AutoSize = false,
            Text = "重复文件清理工具",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Surface,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _sidebar.Controls.Add(_logo);
        _sidebar.Controls.Add(_appName);
        _sidebar.Controls.Add(_appSubtitle);

        string[] labels = { "外观", "扫描器", "排除扩展名", "检查更新", "关于" };
        NavIcon[] icons =
        {
            NavIcon.Appearance, NavIcon.Scanner, NavIcon.Exclusions, NavIcon.Update, NavIcon.About,
        };
        _navItems.Clear();
        for (int i = 0; i < labels.Length; i++)
        {
            var page = (SettingsPage)i;
            var item = new NavItem
            {
                Text = labels[i],
                Icon = icons[i],
            };
            item.Click += (_, _) => SelectPage(page);
            _navItems.Add(item);
            _sidebar.Controls.Add(item);
        }

        _sidebarVersion = new Label
        {
            AutoSize = false,
            Text = "v" + UpdateChecker.CurrentVersion,
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Surface,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _sidebar.Controls.Add(_sidebarVersion);

        _sidebar.Resize += (_, _) => LayoutSidebar();
    }

    private void LayoutSidebar()
    {
        int left = Px(14);
        int width = Math.Max(0, _sidebar.Width - left * 2);
        int top = Px(18);

        _logo.Bounds = new Rectangle(left, top, Px(34), Px(34));
        _appName.Bounds = new Rectangle(left + Px(44), top + Px(1), Math.Max(0, width - Px(44)), Px(20));
        _appSubtitle.Bounds = new Rectangle(left + Px(44), top + Px(19), Math.Max(0, width - Px(44)), Px(16));

        int navTop = top + Px(34) + Px(28);
        for (int i = 0; i < _navItems.Count; i++)
        {
            _navItems[i].Bounds = new Rectangle(
                left, navTop + i * (Px(42) + Px(4)), width, Px(42));
        }

        _sidebarVersion.Bounds = new Rectangle(left, _sidebar.Height - Px(36), width, Px(20));
    }

    private void BuildBottomBar()
    {
        _statusLabel = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Font = Theme.Small,
            ForeColor = Theme.Danger,
            BackColor = Theme.Background,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = string.Empty,
        };
        _btnCancel = new ModernButton
        {
            Text = "取消",
            Kind = ButtonKind.Secondary,
            Size = new Size(Px(88), Px(38)),
            DialogResult = DialogResult.Cancel,
        };
        _btnSave = new ModernButton
        {
            Text = "保存设置",
            Kind = ButtonKind.Primary,
            Size = new Size(Px(112), Px(38)),
        };
        _btnSave.Click += (_, _) => SaveAndClose();

        _bottomBar.Controls.Add(_statusLabel);
        _bottomBar.Controls.Add(_btnCancel);
        _bottomBar.Controls.Add(_btnSave);
        _bottomBar.Resize += (_, _) =>
        {
            int top = (_bottomBar.Height - _btnSave.Height) / 2;
            _btnSave.Location = new Point(_bottomBar.Width - _btnSave.Width, top);
            _btnCancel.Location = new Point(_btnSave.Left - _btnCancel.Width - Px(10), top);
            _statusLabel.Bounds = new Rectangle(
                0, 0, Math.Max(0, _btnCancel.Left - Px(16)), _bottomBar.Height);
        };

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    // ===================== 页面构建 =====================

    private Panel CreatePage()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Visible = false,
        };
    }

    private static Label CreateTitle(string text) => new()
    {
        AutoSize = false,
        Text = text,
        Font = Theme.PageTitle,
        ForeColor = Theme.TextPrimary,
        BackColor = Theme.Background,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Label CreateHint(string text) => new()
    {
        AutoSize = false,
        AutoEllipsis = true,
        Text = text,
        Font = Theme.Small,
        ForeColor = Theme.TextSecondary,
        BackColor = Theme.Background,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Label CreateBody(string text) => new()
    {
        AutoSize = false,
        Text = text,
        Font = Theme.UI,
        ForeColor = Theme.TextPrimary,
        BackColor = Theme.Background,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private Panel BuildAppearancePage()
    {
        Panel page = CreatePage();
        int width = Px(642);

        Label title = CreateTitle("外观");
        title.Bounds = new Rectangle(0, 0, width, Px(34));
        page.Controls.Add(title);

        Label hint = CreateHint("选择界面配色，也可以在固定时间自动切换深色 / 浅色。");
        hint.Bounds = new Rectangle(0, Px(36), width, Px(22));
        page.Controls.Add(hint);

        ThemeMode[] modes = { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System, ThemeMode.Custom };
        string[] captions = { "浅色", "深色", "跟随系统", "自定义时间" };
        int cardWidth = Px(126);
        int cardHeight = Px(118);
        int gap = Px(16);
        _themeCards.Clear();
        for (int i = 0; i < modes.Length; i++)
        {
            ThemeMode mode = modes[i];
            var card = new ThemePreviewCard
            {
                Mode = mode,
                Caption = captions[i],
                Bounds = new Rectangle(i * (cardWidth + gap), Px(70), cardWidth, cardHeight),
            };
            card.Click += (_, _) =>
            {
                if (SelectMode(mode)) PreviewCurrentTheme();
            };
            _themeCards.Add(card);
            page.Controls.Add(card);
        }

        int timeTop = Px(70) + cardHeight + Px(18);
        var timeLabel = CreateBody("深色时段");
        timeLabel.Bounds = new Rectangle(0, timeTop, Px(76), Px(34));
        page.Controls.Add(timeLabel);

        _darkStart = new TimeField { Bounds = new Rectangle(Px(80), timeTop, Px(92), Px(34)) };
        _darkEnd = new TimeField { Bounds = new Rectangle(Px(204), timeTop, Px(92), Px(34)) };
        _darkStart.Value = _result.GetDarkStart();
        _darkEnd.Value = _result.GetDarkEnd();
        _darkStart.ValueCommitted += (_, _) =>
        {
            if (_mode == ThemeMode.Custom) PreviewCurrentTheme();
        };
        _darkEnd.ValueCommitted += (_, _) =>
        {
            if (_mode == ThemeMode.Custom) PreviewCurrentTheme();
        };
        page.Controls.Add(_darkStart);
        page.Controls.Add(_darkEnd);

        var separator = CreateHint("至");
        separator.TextAlign = ContentAlignment.MiddleCenter;
        separator.Bounds = new Rectangle(Px(176), timeTop, Px(24), Px(34));
        page.Controls.Add(separator);

        Label timeHint = CreateHint(
            "「自定义时间」下生效：开始至结束为深色，可跨午夜；开始与结束相同表示全天深色（24 小时制）。");
        timeHint.Bounds = new Rectangle(0, timeTop + Px(46), width, Px(22));
        page.Controls.Add(timeHint);

        Label saveHint = CreateHint("修改后点击「保存设置」生效；标题栏右上角的齿轮按钮可随时重新打开此窗口。");
        saveHint.Bounds = new Rectangle(0, timeTop + Px(74), width, Px(22));
        page.Controls.Add(saveHint);

        return page;
    }

    private static Label CreateSectionLabel(string text) => new()
    {
        AutoSize = false,
        Text = text,
        Font = Theme.UIBold,
        ForeColor = Theme.TextPrimary,
        BackColor = Theme.Background,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private Panel BuildScannerPage()
    {
        Panel page = CreatePage();
        int width = Px(642);

        Label title = CreateTitle("扫描器");
        title.Bounds = new Rectangle(0, 0, width, Px(34));
        page.Controls.Add(title);

        Label hint = CreateHint("选择重复文件比对使用的哈希算法，以及扫描时的提醒与并行设置。");
        hint.Bounds = new Rectangle(0, Px(36), width, Px(22));
        page.Controls.Add(hint);

        // ===== 哈希算法 =====
        Label algorithmLabel = CreateSectionLabel("哈希算法");
        algorithmLabel.Bounds = new Rectangle(0, Px(68), width, Px(24));
        page.Controls.Add(algorithmLabel);

        int chipWidth = Px(168);
        int chipGap = Px(12);
        _algorithmChips.Clear();
        for (int i = 0; i < HashAlgorithmInfo.All.Length; i++)
        {
            HashAlgorithmKind kind = HashAlgorithmInfo.All[i];
            var chip = new OptionChip
            {
                Text = HashAlgorithmInfo.DisplayName(kind),
                Bounds = new Rectangle(i * (chipWidth + chipGap), Px(98), chipWidth, Px(38)),
            };
            chip.Click += (_, _) => SelectAlgorithm(kind);
            _algorithmChips.Add(chip);
            page.Controls.Add(chip);
        }

        _algorithmDescription = CreateHint(string.Empty);
        _algorithmDescription.Bounds = new Rectangle(0, Px(146), width, Px(22));
        page.Controls.Add(_algorithmDescription);

        Label algorithmHint = CreateHint("同一批扫描内所有文件使用同一种算法；文件夹越大，建议选择更快的算法。");
        algorithmHint.Bounds = new Rectangle(0, Px(172), width, Px(20));
        page.Controls.Add(algorithmHint);

        // ===== 大文件夹提醒 =====
        Label warningLabel = CreateSectionLabel("大文件夹提醒");
        warningLabel.Bounds = new Rectangle(0, Px(210), width, Px(24));
        page.Controls.Add(warningLabel);

        _warnToggle = new ToggleSwitch { Bounds = new Rectangle(0, Px(240), Px(46), Px(26)) };
        _warnToggle.Checked = _result.WarnLargeFolder;
        _warnToggle.CheckedChanged += (_, _) => UpdateScannerDimmed();
        page.Controls.Add(_warnToggle);

        Label warnText = CreateBody("超过阈值时弹窗提醒");
        warnText.Bounds = new Rectangle(Px(60), Px(238), Px(250), Px(30));
        page.Controls.Add(warnText);

        _warnGb = new NumberField
        {
            Bounds = new Rectangle(Px(330), Px(236), Px(120), Px(34)),
            Minimum = 1,
            Maximum = 10000,
            Value = _result.LargeFolderWarningGb,
        };
        page.Controls.Add(_warnGb);

        Label gbUnit = CreateBody("GB");
        gbUnit.Bounds = new Rectangle(Px(458), Px(236), Px(40), Px(34));
        page.Controls.Add(gbUnit);

        Label warnHint = CreateHint("默认 10 GB；关闭开关后扫描前不再提示。");
        warnHint.Bounds = new Rectangle(0, Px(282), width, Px(20));
        page.Controls.Add(warnHint);

        // ===== 并行扫描文件数 =====
        Label parallelLabel = CreateSectionLabel("并行扫描文件数");
        parallelLabel.Bounds = new Rectangle(0, Px(318), width, Px(24));
        page.Controls.Add(parallelLabel);

        _autoParallelToggle = new ToggleSwitch { Bounds = new Rectangle(0, Px(348), Px(46), Px(26)) };
        _autoParallelToggle.Checked = _result.AutoParallelism;
        _autoParallelToggle.CheckedChanged += (_, _) => UpdateScannerDimmed();
        page.Controls.Add(_autoParallelToggle);

        Label autoText = CreateBody($"自动（CPU 逻辑核心数 {Environment.ProcessorCount}）");
        autoText.Bounds = new Rectangle(Px(60), Px(346), Px(340), Px(30));
        page.Controls.Add(autoText);

        _parallelCount = new NumberField
        {
            Bounds = new Rectangle(Px(430), Px(344), Px(120), Px(34)),
            Minimum = 1,
            Maximum = 64,
            Value = _result.MaxParallelism,
        };
        page.Controls.Add(_parallelCount);

        Label countUnit = CreateBody("个");
        countUnit.Bounds = new Rectangle(Px(558), Px(344), Px(40), Px(34));
        page.Controls.Add(countUnit);

        Label parallelHint = CreateHint("自动模式会使用当前 CPU 逻辑核心数；手动可设置 1-64。");
        parallelHint.Bounds = new Rectangle(0, Px(390), width, Px(20));
        page.Controls.Add(parallelHint);

        SelectAlgorithm(_algorithm);
        UpdateScannerDimmed();
        return page;
    }

    private void SelectAlgorithm(HashAlgorithmKind kind)
    {
        _algorithm = kind;
        for (int i = 0; i < _algorithmChips.Count; i++)
        {
            _algorithmChips[i].Selected = HashAlgorithmInfo.All[i] == kind;
        }

        if (_algorithmDescription is not null)
        {
            _algorithmDescription.Text =
                $"当前：{HashAlgorithmInfo.DisplayName(kind)} —— {HashAlgorithmInfo.Description(kind)}";
        }
    }

    private void UpdateScannerDimmed()
    {
        if (_warnGb is not null) _warnGb.Dimmed = !_warnToggle.Checked;
        if (_parallelCount is not null) _parallelCount.Dimmed = _autoParallelToggle.Checked;
    }

    private Panel BuildExclusionsPage()
    {
        Panel page = CreatePage();
        int width = Px(642);

        Label title = CreateTitle("全局排除扩展名");
        title.Bounds = new Rectangle(0, 0, width, Px(34));
        page.Controls.Add(title);

        Label hint = CreateHint("以下扩展名的文件不参与扫描，例如 .tmp、.log 这类临时文件。");
        hint.Bounds = new Rectangle(0, Px(36), width, Px(22));
        page.Controls.Add(hint);

        _extensions = new MultilineInput
        {
            Bounds = new Rectangle(0, Px(70), width, Px(300)),
        };
        _extensions.Text = string.Join(Environment.NewLine, _result.ExcludedExtensions);
        _extensions.TextChanged += (_, _) => UpdateExtensionsCount();
        page.Controls.Add(_extensions);

        _extensionsCount = CreateHint(string.Empty);
        _extensionsCount.Bounds = new Rectangle(0, Px(380), width, Px(20));
        page.Controls.Add(_extensionsCount);

        Label hint2 = CreateHint(
            "每行一个，也可用逗号或空格分隔；支持 *.bak 写法；留空表示不排除任何文件。");
        hint2.Bounds = new Rectangle(0, Px(406), width, Px(22));
        page.Controls.Add(hint2);

        Label hint3 = CreateHint("排除规则同时作用于扫描前的快速统计和 SHA-256 扫描。");
        hint3.Bounds = new Rectangle(0, Px(428), width, Px(22));
        page.Controls.Add(hint3);

        UpdateExtensionsCount();
        return page;
    }

    private Panel BuildUpdatePage()
    {
        Panel page = CreatePage();
        int width = Px(642);

        Label title = CreateTitle("检查更新");
        title.Bounds = new Rectangle(0, 0, width, Px(34));
        page.Controls.Add(title);

        Label hint = CreateHint("从 GitHub 获取最新发布版本；只读取公开的版本号，不会上传任何数据。");
        hint.Bounds = new Rectangle(0, Px(36), width, Px(22));
        page.Controls.Add(hint);

        _updateCurrent = CreateBody("当前版本：v" + UpdateChecker.CurrentVersion);
        _updateCurrent.Font = Theme.UIBold;
        _updateCurrent.Bounds = new Rectangle(0, Px(74), width, Px(28));
        page.Controls.Add(_updateCurrent);

        _btnCheckUpdate = new ModernButton
        {
            Text = "检查更新",
            Kind = ButtonKind.Primary,
            Size = new Size(Px(120), Px(38)),
            Location = new Point(0, Px(116)),
        };
        _btnCheckUpdate.Click += BtnCheckUpdate_Click;
        page.Controls.Add(_btnCheckUpdate);

        _btnOpenReleases = new ModernButton
        {
            Text = "打开 GitHub 发布页",
            Kind = ButtonKind.Secondary,
            Size = new Size(Px(168), Px(38)),
            Location = new Point(Px(132), Px(116)),
        };
        _btnOpenReleases.Click += (_, _) => OpenUrl(_releaseUrl);
        page.Controls.Add(_btnOpenReleases);

        _updateStatus = new Label
        {
            AutoSize = false,
            Text = "尚未检查更新。",
            Font = Theme.UI,
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Background,
            TextAlign = ContentAlignment.TopLeft,
        };
        _updateStatus.Bounds = new Rectangle(0, Px(170), width, Px(96));
        page.Controls.Add(_updateStatus);

        return page;
    }

    private Panel BuildAboutPage()
    {
        Panel page = CreatePage();
        int width = Px(642);

        Label title = CreateTitle("关于");
        title.Bounds = new Rectangle(0, 0, width, Px(34));
        page.Controls.Add(title);

        var logo = new AppLogo { Bounds = new Rectangle(0, Px(44), Px(64), Px(64)) };
        page.Controls.Add(logo);

        var name = CreateBody("OneCopy");
        name.Font = Theme.PageTitle;
        name.Bounds = new Rectangle(Px(80), Px(44), Math.Max(0, width - Px(80)), Px(32));
        page.Controls.Add(name);

        var slogan = CreateHint($"v{UpdateChecker.CurrentVersion} · 只留一份，其余清掉。");
        slogan.Font = Theme.UI;
        slogan.ForeColor = Theme.TextSecondary;
        slogan.Bounds = new Rectangle(Px(80), Px(76), Math.Max(0, width - Px(80)), Px(24));
        page.Controls.Add(slogan);

        Label description = new()
        {
            AutoSize = false,
            Text = "OneCopy 是一个 Windows 原生重复文件清理工具：先按文件大小预筛选，再并行计算哈希（SHA-256 / XXH3-128 / BLAKE3），找出内容完全相同的文件。",
            Font = Theme.Small,
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Background,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        description.Bounds = new Rectangle(0, Px(124), width, Px(44));
        page.Controls.Add(description);

        int rowTop = Px(180);
        int rowHeight = Px(34);
        AddInfoRow(page, rowTop, "开源协议", "GNU GPL v3.0 或更高版本", width);
        AddInfoRow(page, rowTop + rowHeight, "作者", "starlight196", width);
        AddInfoRow(page, rowTop + rowHeight * 2, "项目主页", "github.com/starlight196/OneCopy", width, UpdateChecker.RepositoryUrl);

        Label copyright = CreateHint("Copyright (C) 2026 starlight196 · 本程序基于 GPL-3.0-or-later 发布。");
        copyright.Bounds = new Rectangle(0, rowTop + rowHeight * 3 + Px(12), width, Px(22));
        page.Controls.Add(copyright);

        return page;
    }

    private void AddInfoRow(Panel page, int top, string label, string value, int width, string? linkUrl = null)
    {
        var labelControl = CreateHint(label);
        labelControl.Bounds = new Rectangle(0, top, Px(90), Px(28));
        page.Controls.Add(labelControl);

        Label valueControl = CreateBody(value);
        if (linkUrl is not null)
        {
            valueControl.ForeColor = Theme.Accent;
            valueControl.Cursor = Cursors.Hand;
            valueControl.Click += (_, _) => OpenUrl(linkUrl);
        }
        valueControl.Bounds = new Rectangle(Px(96), top, Math.Max(0, width - Px(96)), Px(28));
        page.Controls.Add(valueControl);
    }

    // ===================== 页面切换 =====================

    private void SelectPage(SettingsPage page)
    {
        _page = page;
        for (int i = 0; i < _navItems.Count; i++)
        {
            _navItems[i].Selected = (SettingsPage)i == page;
        }
        foreach ((SettingsPage key, Panel value) in _pages)
        {
            value.Visible = key == page;
        }
    }

    private bool SelectMode(ThemeMode mode)
    {
        bool changed = _mode != mode;
        _mode = mode;
        foreach (ThemePreviewCard card in _themeCards)
        {
            card.Selected = card.Mode == mode;
        }
        return changed;
    }

    /// <summary>
    /// 点击外观卡片后立即切换整个程序的明暗主题（不必等保存）：
    /// 先按当前编辑值算出目标主题，切换 Theme，再通知宿主窗口换肤并重建本窗口。
    /// </summary>
    private void PreviewCurrentTheme()
    {
        if (_previewing || _rebuilding || IsDisposed) return;
        _previewing = true;
        try
        {
            AppSettings probe = _result.Clone();
            probe.ThemeMode = _mode;
            if (_mode == ThemeMode.Custom)
            {
                if (!_darkStart.TryCommit(out TimeOnly start) || !_darkEnd.TryCommit(out TimeOnly end))
                    return;
                probe.SetDarkRange(start, end);
            }
            else
            {
                probe.SetDarkRange(_darkStart.Value, _darkEnd.Value);
            }

            bool dark = probe.ResolveDark(DateTime.Now);
            if (dark == Theme.DarkMode) return;

            Theme.SetDarkMode(dark);
            PreviewThemeRequested?.Invoke(this, EventArgs.Empty);
            BeginInvoke(new Action(RebuildForThemeChange));
        }
        finally
        {
            _previewing = false;
        }
    }

    /// <summary>主题切换后重建本窗口，让所有控件使用新配色，同时保留编辑状态。</summary>
    private void RebuildForThemeChange()
    {
        if (IsDisposed || _rebuilding) return;
        _rebuilding = true;
        try
        {
            TimeOnly start = _darkStart.Value;
            TimeOnly end = _darkEnd.Value;
            string extensionsText = _extensions.Text;
            string statusText = _statusLabel.Text;
            string updateStatusText = _updateStatus.Text;
            SettingsPage page = _page;
            HashAlgorithmKind algorithm = _algorithm;
            bool warnLargeFolder = _warnToggle.Checked;
            int warnGb = _warnGb.Value;
            bool autoParallelism = _autoParallelToggle.Checked;
            int parallelCount = _parallelCount.Value;

            SuspendLayout();
            for (int i = Controls.Count - 1; i >= 0; i--)
            {
                Control control = Controls[i];
                Controls.RemoveAt(i);
                control.Dispose();
            }
            BuildUI();
            ResumeLayout(performLayout: true);

            _darkStart.Value = start;
            _darkEnd.Value = end;
            _extensions.Text = extensionsText;
            _statusLabel.Text = statusText;
            _updateStatus.Text = updateStatusText;
            SelectAlgorithm(algorithm);
            _warnToggle.Checked = warnLargeFolder;
            _warnGb.Value = warnGb;
            _autoParallelToggle.Checked = autoParallelism;
            _parallelCount.Value = parallelCount;
            UpdateScannerDimmed();
            SelectPage(page);
            WindowChrome.Apply(this);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private void UpdateExtensionsCount()
    {
        if (_extensionsCount is null) return;
        List<string> normalized = AppSettings.ParseExtensionsText(_extensions.Text, out List<string> invalid);
        _extensionsCount.Text = invalid.Count > 0
            ? $"当前有 {invalid.Count} 个无法识别的条目，保存时会提示。"
            : $"当前填写 {normalized.Count} 个扩展名。";
        _extensionsCount.ForeColor = invalid.Count > 0 ? Theme.Danger : Theme.TextSecondary;
    }

    // ===================== 保存 =====================

    private bool TrySave(out string error, out SettingsPage page)
    {
        error = string.Empty;
        page = SettingsPage.Appearance;

        TimeOnly start;
        TimeOnly end;
        if (_mode == ThemeMode.Custom)
        {
            if (!_darkStart.TryCommit(out start) || !_darkEnd.TryCommit(out end))
            {
                error = "时间格式应为 24 小时制，例如 19:00。";
                page = SettingsPage.Appearance;
                return false;
            }
        }
        else
        {
            start = _darkStart.Value;
            end = _darkEnd.Value;
        }

        List<string> extensions = AppSettings.ParseExtensionsText(_extensions.Text, out List<string> invalid);
        if (invalid.Count > 0)
        {
            string list = string.Join("、", invalid.Take(4));
            if (invalid.Count > 4) list += " 等";
            error = $"无法识别的扩展名：{list}";
            page = SettingsPage.Exclusions;
            return false;
        }

        _warnGb.TryCommit();
        _parallelCount.TryCommit();

        _result.ThemeMode = _mode;
        _result.SetDarkRange(start, end);
        _result.ExcludedExtensions = extensions;
        _result.HashAlgorithm = _algorithm;
        _result.WarnLargeFolder = _warnToggle.Checked;
        _result.LargeFolderWarningGb = _warnGb.Value;
        _result.AutoParallelism = _autoParallelToggle.Checked;
        _result.MaxParallelism = _parallelCount.Value;
        return true;
    }

    private void SaveAndClose()
    {
        if (!TrySave(out string error, out SettingsPage page))
        {
            SelectPage(page);
            _statusLabel.Text = error;
            return;
        }

        _statusLabel.Text = string.Empty;
        DialogResult = DialogResult.OK;
        Close();
    }

    // ===================== 检查更新 =====================

    private async void BtnCheckUpdate_Click(object? sender, EventArgs e)
    {
        _btnCheckUpdate.Enabled = false;
        _btnCheckUpdate.Text = "正在检查…";
        _updateStatus.ForeColor = Theme.TextSecondary;
        _updateStatus.Text = "正在连接 GitHub…";

        try
        {
            UpdateCheckResult result = await UpdateChecker.CheckAsync();
            if (IsDisposed) return;

            if (result.State == UpdateCheckState.Newer)
            {
                _updateStatus.ForeColor = Theme.Accent;
                if (!string.IsNullOrEmpty(result.DownloadUrl)) _releaseUrl = result.DownloadUrl!;
            }
            else if (result.State == UpdateCheckState.UpToDate)
            {
                _updateStatus.ForeColor = Theme.TextSecondary;
            }
            else
            {
                _updateStatus.ForeColor = Theme.Danger;
            }

            _updateStatus.Text = result.Message;
        }
        finally
        {
            if (!IsDisposed)
            {
                _btnCheckUpdate.Enabled = true;
                _btnCheckUpdate.Text = "检查更新";
            }
        }
    }

    private static void OpenUrl(string url)
    {
        // 只允许打开 GitHub 的 http/https 链接：即使链接被伪造，也不会执行本地程序或打开本地文件。
        if (!UpdateChecker.IsAllowedUrl(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 无法打开浏览器时静默忽略
        }
    }

#if DEBUG
    // ===== 自检辅助（仅 Debug 构建） =====

    internal void ShowPageForTest(SettingsPage page) => SelectPage(page);

    internal SettingsPage CurrentPageForTest => _page;

    internal int NavItemCountForTest => _navItems.Count;

    internal bool PageVisibleForTest(SettingsPage page) => _pages[page].Visible;

    internal string PageTitleForTest(SettingsPage page)
    {
        foreach (Control control in _pages[page].Controls)
        {
            if (control is Label label && label.Font == Theme.PageTitle) return label.Text;
        }
        return string.Empty;
    }

    internal void SelectModeForTest(ThemeMode mode) => SelectMode(mode);

    /// <summary>自检用：选中哈希算法。</summary>
    internal void SelectAlgorithmForTest(HashAlgorithmKind kind) => SelectAlgorithm(kind);

    /// <summary>自检用：设置扫描器页的提醒与并行参数。</summary>
    internal void SetScannerForTest(bool warnLargeFolder, int warnGb, bool autoParallelism, int parallelCount)
    {
        _warnToggle.Checked = warnLargeFolder;
        _warnGb.Value = warnGb;
        _autoParallelToggle.Checked = autoParallelism;
        _parallelCount.Value = parallelCount;
        UpdateScannerDimmed();
    }

    internal string AlgorithmDescriptionForTest => _algorithmDescription.Text;

    internal HashAlgorithmKind AlgorithmForTest => _algorithm;

    /// <summary>模拟点击外观卡片（含主题实时预览）。</summary>
    internal void SelectModeWithPreviewForTest(ThemeMode mode)
    {
        if (SelectMode(mode)) PreviewCurrentTheme();
    }

    internal void SetTimesForTest(TimeOnly start, TimeOnly end)
    {
        _darkStart.Value = start;
        _darkEnd.Value = end;
    }

    internal void SetExtensionsForTest(string text) => _extensions.Text = text;

    internal string ExtensionsForTest => _extensions.Text;

    internal string ExtensionsCountForTest => _extensionsCount.Text;

    internal bool SaveForTest()
    {
        if (!TrySave(out string error, out SettingsPage page))
        {
            SelectPage(page);
            _statusLabel.Text = error;
            return false;
        }

        _statusLabel.Text = string.Empty;
        return true;
    }

    internal string ErrorTextForTest => _statusLabel.Text;

    /// <summary>预览用：让检查更新页显示一个"发现新版本"的示例结果。</summary>
    internal void PreviewNewerResultForTest()
    {
        _updateStatus.ForeColor = Theme.Accent;
        _updateStatus.Text =
            "发现新版本 v1.4.1，可打开发布页下载。\n" +
            "https://github.com/starlight196/OneCopy/releases";
    }
#endif
}
