# OneCopy 重复文件清理工具

> 只留一份，其余清掉。 ｜ 当前版本：**v1.5.1** ｜ [更新日志](CHANGELOG.md) ｜ [GPL-3.0 许可证](LICENSE)

**OneCopy** 是一个 Windows 原生桌面程序：扫描指定目录下内容完全相同的文件，按哈希分组展示，帮你只保留一份、安全地清掉多余副本。

## 功能特性

- 🎨 **自绘现代界面**：无边框圆角窗口 + 自绘标题栏（应用标志、版本号、设置、窗口按钮），主窗口与所有对话框统一圆角外观，不受系统主题/第三方美化影响
- ⚙️ **设置中心**（标题栏齿轮按钮或 `Ctrl+,`）：左侧导航 + 右侧内容
  - **外观**：浅色 / 深色 / 跟随系统 / 自定义时间，四张主题预览卡片；点击卡片立即切换明暗主题（保存后才持久化，取消会恢复原主题）；自定义时间使用 24 小时制深色时段，支持跨午夜，到点自动切换
  - **扫描器**：哈希算法（SHA-256 默认 / XXH3-128 / BLAKE3）、大文件夹提醒阈值（默认 10 GB，可修改或关闭）、并行扫描文件数（自动使用 CPU 核心数或手动 1-64）
  - **全局排除扩展名**：`.tmp`、`.log` 等文件不参与扫描，快速统计与哈希扫描同时生效，实时显示已填写数量
  - **检查更新**：读取 GitHub 最新 Release 版本号，提示是否有新版本，可直接打开发布页
  - **关于**：版本号、开源协议、作者与项目主页
  - 设置自动保存到 `%AppData%\OneCopy\settings.json`，无写权限时自动使用默认值
- 🔍 **三种哈希算法**：SHA-256（SHA-2，CNG 硬件加速）/ XXH3-128（xxHash，极快非加密）/ BLAKE3（加密级且快），均通过官方测试向量验证
- 🚦 **扫描控制**：扫描过程中可随时**暂停 / 继续**（真正挂起枚举与哈希线程）或**停止扫描**；`Esc` 快捷键停止
- ⚠ **大文件夹提醒**：扫描前统计文件数 / 大小，超过阈值时弹窗确认，可调整阈值或关闭
- 🧵 **并行策略**：默认按 CPU 逻辑核心数并行哈希，也可在设置中手动指定并行文件数
- 🌗 **深色 / 浅色主题**：所有界面元素（含列表、复选框、进度条、滚动条、设置窗口）同步换肤
- 📁 原生 Windows 文件夹选择对话框
- 🗂 先按文件大小分组 → 再对同大小文件并行计算哈希，高效筛选
- 🚀 并行哈希计算，利用多核 CPU；进度上报节流，界面不卡顿
- 🗂 分组列表：哈希组 → 文件两行信息（文件名 / 大小 / 修改时间 / 所在目录），分组可折叠
- ☑ 圆角复选框：单个勾选与整组勾选（半选状态），勾选状态实时同步到统计栏与删除按钮
- ⚡ 快捷操作：保留每组第一个 / 保留最新文件；全选 / 取消全选（`Ctrl+A`）；`Delete` 键直接删除
- ⚠ 整组全部勾选时给出醒目提示；删除前二次确认，操作不可恢复
- 📊 实时统计已勾选文件数与可释放空间
- 🖱 双击文件行可在资源管理器中定位该文件
- 📋 扫描前弹出文件夹统计确认窗口（按扩展名分组，并显示算法与并行数）

## 技术栈

- **语言**: C# (.NET 8)
- **UI**: Windows Forms（全部自绘：标题栏、圆角按钮、进度条、分组列表、表格）
- **窗口**: 无边框窗口 + `ChromeForm` 基类统一 DWM 圆角 / 阴影，Win32 `WM_NCCALCSIZE` / `WM_NCHITTEST` 自绘标题栏与缩放边界
- **哈希**: SHA-256（CNG 硬件加速）/ XXH3-128 / BLAKE3（后两者为托管自实现，官方测试向量校验）
- **并行**: `Parallel.ForEach` + `Task`

## 编译运行

```bash
# 运行
dotnet run -c Release

# 生成真正的单个可执行文件（原生库一并打包进 exe，不生成 pdb）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o ./publish
```

> 开发调试（Debug 构建）：
> - `dotnet run -c Debug -- --preview 输出.png [populated|empty|scanning|dialog|settings|settings-scanner|settings-exclusions|settings-update|settings-about] [dark|light|system]`：把窗口显示在屏幕外并用 `PrintWindow` 截取真实渲染效果
> - `dotnet run -c Debug -- --smoke 输出.png`：真实启动窗口，自检缩放边界 / 最大化布局 / 标题栏按钮 / 暂停与停止按钮 / 勾选联动 / 确认对话框 / 设置窗口各页面与外观实时预览 / 哈希算法测试向量 / 暂停闸门 / 窗口圆角样式 / 排除扩展名扫描并截图
> - `dotnet run -c Debug -- --hash-test 输出.log`：用官方测试向量校验 SHA-256 / XXH3-128 / BLAKE3，并检查流式与一次性计算结果一致
> - `dotnet run -c Debug -- --screen-capture 输出.png [settings|main] [dark|light|system]`：真正显示到屏幕上截图，用于检查 DWM 圆角（窗口会短暂出现）

## 项目结构

```
OneCopy/
├── Program.cs              # 入口（含 Debug 预览 / 自检入口）
├── Models.cs               # 数据模型
├── AppSettings.cs          # 应用设置：外观 / 扫描器 / 排除扩展名 / 持久化
├── UpdateChecker.cs        # 检查更新：读取 GitHub 最新 Release 并比较版本
├── HashAlgorithms.cs       # 哈希算法枚举、显示信息与文件哈希计算入口
├── XxHash3_128.cs          # XXH3-128（xxHash 128 位）托管实现
├── Blake3.cs               # BLAKE3（32 字节输出）托管实现
├── FileScanner.cs          # 扫描引擎（支持暂停 / 排除扩展名 / 算法与并行设置）
├── MainForm.cs             # 主窗口：无边框布局、扫描流程、暂停/停止、删除与快捷键
├── ChromeForm.cs           # 统一窗口基类：DWM 圆角 / 阴影 / 缩放边框 / 固定尺寸对话框
├── Theme.cs                # 主题：深浅色调色板、字体、自绘按钮 / 进度条 / 卡片
├── TitleBar.cs             # 自绘标题栏、设置（简约齿轮）/ 窗口按钮、DWM 圆角 / 深色边框
├── SettingsForm.cs         # 设置窗口：左侧导航（外观 / 扫描器 / 排除扩展名 / 检查更新 / 关于）
├── SettingsControls.cs     # 设置窗口用输入控件：时间输入框、多行输入框、数字输入框
├── SettingsParts.cs        # 设置窗口用自绘部件：侧边栏、导航项、主题卡片、开关、选项按钮
├── DuplicateListView.cs    # 现代分组列表（自绘行、复选框、折叠、自定义滚动条）
├── ExtensionStatsView.cs   # 扫描确认窗口里的扩展名统计表格
├── NativeMethods.cs        # Win32 / DWM 互操作
├── PreviewRenderer.cs      # Debug 预览 / 自检辅助（Release 不编译）
├── HashSelfTest.cs         # Debug 哈希算法测试向量自检（Release 不编译）
├── CHANGELOG.md            # 更新日志
├── LICENSE                 # GNU GPL v3.0 许可证
├── app.ico                 # 多尺寸应用图标（exe 文件 / 窗口 / 任务栏）
├── OneCopy.csproj
└── publish/                # 发布输出（git 忽略）
    ├── OneCopy.exe                     # 单文件可执行程序（v1.5.1）
    └── OneCopy-v1.5.1.zip              # 发布压缩包
```

## 许可

Copyright (C) 2026 starlight196

本项目基于 **GNU General Public License v3.0（或更高版本）** 开源，详见 [LICENSE](LICENSE)。

OneCopy is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

OneCopy is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
