# OneCopy 重复文件清理工具

> 只留一份，其余清掉。 ｜ 当前版本：**v1.5.2** ｜ [更新日志](CHANGELOG.md) ｜ [GPL-3.0 许可证](LICENSE)

**OneCopy** 是一个 Windows 原生桌面程序（C# / .NET 8 / WinForms，界面全部自绘）：扫描目录 → 按内容哈希分组 → 手动勾选 → 一次删掉多余副本。

## 功能

- **三种哈希算法**：SHA-256（默认）/ XXH3-128 / BLAKE3，均通过官方测试向量校验
- **现代自绘界面**：无边框圆角窗口，浅色 / 深色 / 跟随系统 / 自定义时段主题
- **扫描控制**：随时暂停 / 继续 / 停止，可调并行数，大文件夹扫描前提醒
- **筛选与清理**：分组折叠、整组勾选、保留第一个 / 保留最新、全局排除扩展名
- **设置中心**：算法、提醒阈值、并行数、排除扩展名、关于与检查更新

## 安全性

- 文件名只作为文件系统路径处理，**不会被当作命令执行**；「在资源管理器中定位」通过 Windows Shell 接口完成，不拼接命令行
- 不调用 `cmd` / PowerShell；检查更新只读 GitHub API，不下载、不自动执行任何东西，链接限定 GitHub 域名
- 无遥测、不写注册表、无需管理员权限
- 删除前需手动勾选并二次确认；删除为永久删除，不进回收站

## 使用

从 [Releases](https://github.com/starlight196/OneCopy/releases) 下载压缩包，解压后直接运行 `OneCopy.exe`（单文件，无需安装 .NET）：

1. 选择要扫描的文件夹
2. 等待扫描完成
3. 勾选要删除的副本
4. 点击删除

## 构建

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o ./publish
```

> Debug 构建提供 `--smoke` / `--preview` / `--hash-test` 自检入口，用法见源码注释。

## 许可

Copyright (C) 2026 starlight196 · 本项目基于 [GNU GPL v3.0 或更高版本](LICENSE) 开源。
