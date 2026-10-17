// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

namespace OneCopy;

/// <summary>
/// 表示扫描到的单个文件信息。
/// </summary>
public class FileItem
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastWriteTime { get; set; }
    public string? Hash { get; set; }

    /// <summary>文件名（不含路径）</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>所在目录</summary>
    public string DirectoryName => Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>是否被用户勾选为待删除</summary>
    public bool IsChecked { get; set; }

    /// <summary>人类可读的文件大小</summary>
    public string FileSizeDisplay => FormatSize(FileSize);

    public static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// 表示一组内容完全相同的重复文件。
/// </summary>
public class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;
    public List<FileItem> Files { get; set; } = new();

    /// <summary>截断显示的哈希值</summary>
    public string HashShort => Hash.Length > 16 ? Hash[..16] + "…" : Hash;

    /// <summary>该组文件数量</summary>
    public int FileCount => Files.Count;

    /// <summary>
    /// 可释放空间：假设保留 1 个文件，删除其余 (n-1) 个文件可释放的空间。
    /// </summary>
    public long WastedSpace => Files.Count > 1 ? Files[0].FileSize * (Files.Count - 1) : 0;

    /// <summary>人类可读的可释放空间</summary>
    public string WastedSpaceDisplay => FileItem.FormatSize(WastedSpace);

    /// <summary>TreeView 中显示的文本</summary>
    public string DisplayText => $"{HashShort}  [{FileCount} 个文件, {WastedSpaceDisplay} 可释放]";
}
