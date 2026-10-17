# OneCopy 重复文件清理工具

> 只留一份，其余清掉。 ｜ 当前版本：**v1.2.0** ｜ [更新日志](CHANGELOG.md) ｜ [GPL-3.0 许可证](LICENSE)

**OneCopy** 是一个 Windows 原生桌面程序：扫描指定目录下内容完全相同的文件，按哈希分组展示，帮你只保留一份、安全地清掉多余副本。

## 功能特性

- 🎨 **自绘现代界面**：无边框圆角窗口 + 自绘标题栏（应用标志、版本号、主题切换、窗口按钮），不受系统主题/第三方美化影响
- 🌗 **深色 / 浅色主题**：启动时跟随系统，标题栏一键切换，所有界面元素（含列表、复选框、进度条、滚动条）同步换肤
- 📁 原生 Windows 文件夹选择对话框
- 🔍 先按文件大小分组 → 再对同大小文件并行计算 SHA-256，高效筛选
- 🚀 并行哈希计算，利用多核 CPU 和 Windows CNG 硬件加速；进度上报节流，界面不卡顿
- 🗂 分组列表：哈希组 → 文件两行信息（文件名 / 大小 / 修改时间 / 所在目录），分组可折叠
- ☑ 圆角复选框：单个勾选与整组勾选（半选状态），勾选状态实时同步到统计栏与删除按钮
- ⚡ 快捷操作：保留每组第一个 / 保留最新文件；全选 / 取消全选（`Ctrl+A`）；`Delete` 键直接删除
- ⚠ 整组全部勾选时给出醒目提示；删除前二次确认，操作不可恢复
- 📊 实时统计已勾选文件数与可释放空间
- 🖱 双击文件行可在资源管理器中定位该文件
- 📋 扫描前弹出文件夹统计确认窗口（按扩展名分组）

## 技术栈

- **语言**: C# (.NET 8)
- **UI**: Windows Forms（全部自绘：标题栏、圆角按钮、进度条、分组列表、表格）
- **窗口**: 无边框窗口 + Win32 `WM_NCCALCSIZE` / `WM_NCHITTEST` 自绘标题栏与缩放边界，DWM 圆角与深色边框
- **哈希**: SHA-256（CNG 硬件加速）
- **并行**: `Parallel.ForEach` + `Task`

## 编译运行

```bash
# 运行
dotnet run -c Release

# 生成真正的单个可执行文件（原生库一并打包进 exe，不生成 pdb）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o ./publish
```

> 开发调试（Debug 构建）：
> - `dotnet run -c Debug -- --preview 输出.png [populated|empty|dialog] [dark|light|system]`：把窗口显示在屏幕外并用 `PrintWindow` 截取真实渲染效果
> - `dotnet run -c Debug -- --smoke 输出.png`：真实启动窗口，自检缩放边界 / 最大化布局 / 标题栏按钮 / 勾选联动 / 确认对话框 / 主题切换并截图

## 项目结构

```
OneCopy/
├── Program.cs              # 入口（含 Debug 预览 / 自检入口）
├── Models.cs               # 数据模型
├── FileScanner.cs          # 扫描引擎
├── MainForm.cs             # 主窗口：无边框布局、扫描流程、删除与快捷键
├── Theme.cs                # 主题：深浅色调色板、字体、自绘按钮 / 进度条 / 卡片
├── TitleBar.cs             # 自绘标题栏、窗口按钮、DWM 圆角 / 深色边框
├── DuplicateListView.cs    # 现代分组列表（自绘行、复选框、折叠、自定义滚动条）
├── ExtensionStatsView.cs   # 扫描确认窗口里的扩展名统计表格
├── NativeMethods.cs        # Win32 / DWM 互操作
├── PreviewRenderer.cs      # Debug 预览 / 自检辅助（Release 不编译）
├── CHANGELOG.md            # 更新日志
├── LICENSE                 # GNU GPL v3.0 许可证
├── OneCopy.csproj
└── publish/                # 发布输出（git 忽略）
    ├── OneCopy.exe                     # 单文件可执行程序（v1.2.0）
    └── OneCopy-v1.2.0.zip              # 发布压缩包
```

## 许可

Copyright (C) 2026 starlight196

本项目基于 **GNU General Public License v3.0（或更高版本）** 开源，详见 [LICENSE](LICENSE)。

OneCopy is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

OneCopy is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
