// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace OneCopy;

/// <summary>
/// 扫描进度信息，通过 IProgress&lt;ScanProgress&gt; 报告给 UI。
/// </summary>
public class ScanProgress
{
    /// <summary>0=正在枚举文件, 1=正在计算哈希, 2=扫描完成</summary>
    public int Phase { get; set; }
    /// <summary>待处理文件总数</summary>
    public int TotalFiles { get; set; }
    /// <summary>已处理文件数</summary>
    public int ProcessedFiles { get; set; }
    /// <summary>当前正在处理的文件名</summary>
    public string CurrentFile { get; set; } = string.Empty;
    /// <summary>完成百分比 0-100</summary>
    public int Percentage => TotalFiles > 0 ? (int)((double)ProcessedFiles / TotalFiles * 100) : 0;
}

/// <summary>
/// 核心扫描引擎：递归枚举文件 → 按大小分组 → 并行 SHA-256 哈希 → 找出重复组。
/// </summary>
public class FileScanner
{
    /// <summary>大文件读取缓冲区大小 (1 MB)</summary>
    private const int HashBufferSize = 1_048_576;

    /// <summary>全局排除的扩展名集合（含前导点，大小写不敏感）。</summary>
    public HashSet<string> ExcludedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>暂停闸门：置位时扫描继续，Reset 时各工作线程在下一个检查点挂起。</summary>
    private readonly ManualResetEventSlim _pauseGate = new(initialState: true);

    /// <summary>当前是否处于暂停状态。</summary>
    public bool IsPaused => !_pauseGate.IsSet;

    /// <summary>暂停扫描（当前正在处理的文件会完成，之后的工作挂起）。</summary>
    public void Pause() => _pauseGate.Reset();

    /// <summary>继续扫描。</summary>
    public void Resume() => _pauseGate.Set();

    private static bool IsExcluded(string extension, HashSet<string>? excluded) =>
        excluded is { Count: > 0 } && extension.Length > 0 && excluded.Contains(extension);

    /// <summary>
    /// 扫描指定目录，返回所有重复文件组。
    /// </summary>
    /// <param name="rootPath">要扫描的根目录</param>
    /// <param name="progress">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按可释放空间降序排列的重复文件组列表</returns>
    public async Task<List<DuplicateGroup>> ScanAsync(
        string rootPath,
        IProgress<ScanProgress> progress,
        CancellationToken cancellationToken)
    {
        // ===== 阶段 0：递归枚举所有文件 =====
        progress.Report(new ScanProgress { Phase = 0, CurrentFile = "正在枚举文件…" });

        var allFiles = new List<FileInfo>();
        int enumerated = 0;
        HashSet<string> excluded = ExcludedExtensions;
        await Task.Run(() => EnumerateFiles(rootPath, allFiles, cancellationToken, excluded, () =>
        {
            // 每个文件检查一次暂停 / 取消，暂停时工作线程在此挂起
            _pauseGate.Wait(cancellationToken);
            enumerated++;
            if ((enumerated & 255) == 0)
            {
                progress.Report(new ScanProgress
                {
                    Phase = 0,
                    ProcessedFiles = enumerated,
                    CurrentFile = "正在枚举文件…",
                });
            }
        }), cancellationToken);

        if (allFiles.Count == 0)
            return new List<DuplicateGroup>();

        // ===== 阶段 1：按文件大小分组（快速预筛选），只对大小相同的组计算哈希 =====
        // 按文件大小分组，忽略只有 1 个文件的大小组（不可能有重复）
        var sizeGroups = allFiles
            .GroupBy(f => f.Length)
            .Where(g => g.Count() >= 2)
            .ToList();

        // 实际需要哈希的文件总数（进度条以此为分母，避免永远到不了 100%）
        int totalToHash = sizeGroups.Sum(g => g.Count());
        if (totalToHash == 0)
        {
            // 没有同大小文件，不可能存在重复，直接结束
            progress.Report(new ScanProgress { Phase = 2, TotalFiles = allFiles.Count, ProcessedFiles = allFiles.Count });
            return new List<DuplicateGroup>();
        }

        progress.Report(new ScanProgress
        {
            Phase = 1,
            TotalFiles = totalToHash,
            ProcessedFiles = 0,
            CurrentFile = "正在计算 SHA-256…",
        });

        var scannedCount = 0;

        // 线程安全的重复组收集容器
        var duplicateGroups = new ConcurrentBag<DuplicateGroup>();

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
        };

        // 并行处理每个大小组
        await Task.Run(() =>
        {
            Parallel.ForEach(sizeGroups, parallelOptions, sizeGroup =>
            {
                var filesInGroup = sizeGroup.ToList();

                // 对该大小组内的所有文件计算哈希，再按哈希分组
                var hashDict = new Dictionary<string, List<FileItem>>();

                foreach (var fileInfo in filesInGroup)
                {
                    // 暂停 / 取消检查点：暂停期间工作线程在此挂起
                    _pauseGate.Wait(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    string? hash = null;
                    try
                    {
                        hash = ComputeSha256(fileInfo.FullName);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // 跳过无法读取的文件（权限不足、被锁定等）
                    }

                    if (hash is not null)
                    {
                        var fileItem = new FileItem
                        {
                            FilePath = fileInfo.FullName,
                            FileSize = fileInfo.Length,
                            LastWriteTime = fileInfo.LastWriteTime,
                            Hash = hash
                        };

                        lock (hashDict)
                        {
                            if (!hashDict.TryGetValue(hash, out var list))
                            {
                                list = new List<FileItem>();
                                hashDict[hash] = list;
                            }
                            list.Add(fileItem);
                        }
                    }

                    // 更新进度：每 16 个文件上报一次，避免大量小文件时把 UI 消息队列刷爆
                    int done = Interlocked.Increment(ref scannedCount);
                    if (done == totalToHash || (done & 15) == 0)
                    {
                        progress.Report(new ScanProgress
                        {
                            Phase = 1,
                            TotalFiles = totalToHash,
                            ProcessedFiles = done,
                            CurrentFile = fileInfo.Name,
                        });
                    }
                }

                // 找出该大小组中哈希相同的文件（≥2 个才算重复）
                lock (duplicateGroups)
                {
                    foreach (var kvp in hashDict)
                    {
                        if (kvp.Value.Count >= 2)
                        {
                            // 按路径排序以保证展示顺序一致
                            kvp.Value.Sort((a, b) => string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));
                            duplicateGroups.Add(new DuplicateGroup
                            {
                                Hash = kvp.Key,
                                Files = kvp.Value
                            });
                        }
                    }
                }
            });
        }, cancellationToken);

        // 阶段 2：完成
        progress.Report(new ScanProgress
        {
            Phase = 2,
            TotalFiles = totalToHash,
            ProcessedFiles = totalToHash,
            CurrentFile = "正在整理结果…",
        });

        // 按可释放空间降序返回
        return duplicateGroups
            .OrderByDescending(g => g.WastedSpace)
            .ThenBy(g => g.Hash)
            .ToList();
    }

    /// <summary>
    /// 递归枚举目录下所有文件，跳过重解析点（符号链接等）与全局排除的扩展名，忽略无权限目录。
    /// </summary>
    private static void EnumerateFiles(
        string path,
        List<FileInfo> fileList,
        CancellationToken ct,
        HashSet<string>? excluded,
        Action? onFound = null)
    {
        try
        {
            // 枚举文件
            foreach (string filePath in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(filePath);
                    if (IsExcluded(fi.Extension, excluded)) continue;
                    fileList.Add(fi);
                    onFound?.Invoke();
                }
                catch
                {
                    // 跳过无法访问的文件
                }
            }

            // 递归枚举子目录（跳过 Junction / SymbolicLink 防止无限循环）
            foreach (string dirPath in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var di = new DirectoryInfo(dirPath);
                    if ((di.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        continue;
                    EnumerateFiles(dirPath, fileList, ct, excluded, onFound);
                }
                catch
                {
                    // 跳过无权限或出错的目录
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (PathTooLongException) { }
        catch (DirectoryNotFoundException) { }
    }

    /// <summary>
    /// 使用流式读取 + SHA-256 计算文件哈希。
    /// Windows 上 SHA256.Create() 会使用 CNG 硬件加速。
    /// 使用 1MB 缓冲区减少大文件的 I/O 开销。
    /// </summary>
    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: HashBufferSize,
            options: FileOptions.SequentialScan); // 提示 OS 顺序读取
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 快速扫描目录，按扩展名统计文件数和大小（不计算哈希，很快）。
    /// </summary>
    public static FileSummary GetFileSummary(
        string rootPath,
        CancellationToken ct = default,
        HashSet<string>? excludedExtensions = null)
    {
        var summary = new FileSummary();
        var extMap = new Dictionary<string, (int Count, long Size)>(StringComparer.OrdinalIgnoreCase);

        EnumerateFilesForSummary(rootPath, summary, extMap, excludedExtensions, ct);

        summary.Extensions = extMap
            .OrderByDescending(kv => kv.Value.Size)
            .Select(kv => new ExtensionGroup
            {
                Extension = string.IsNullOrEmpty(kv.Key) ? "(无扩展名)" : kv.Key,
                Count = kv.Value.Count,
                TotalSize = kv.Value.Size
            })
            .ToList();

        return summary;
    }

    private static void EnumerateFilesForSummary(
        string path, FileSummary summary,
        Dictionary<string, (int Count, long Size)> extMap,
        HashSet<string>? excluded,
        CancellationToken ct)
    {
        try
        {
            foreach (string filePath in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(filePath);
                    if (IsExcluded(fi.Extension, excluded)) continue;
                    long size = fi.Length;
                    summary.TotalFiles++;
                    summary.TotalSize += size;

                    string ext = fi.Extension;
                    if (extMap.TryGetValue(ext, out var val))
                        extMap[ext] = (val.Count + 1, val.Size + size);
                    else
                        extMap[ext] = (1, size);
                }
                catch { }
            }

            foreach (string dirPath in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var di = new DirectoryInfo(dirPath);
                    if ((di.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        continue;
                    EnumerateFilesForSummary(dirPath, summary, extMap, excluded, ct);
                }
                catch { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (PathTooLongException) { }
        catch (DirectoryNotFoundException) { }
    }
}

/// <summary>
/// 快速扫描结果：文件总数、总大小、按扩展名分组统计。
/// </summary>
public class FileSummary
{
    public int TotalFiles { get; set; }
    public long TotalSize { get; set; }
    public List<ExtensionGroup> Extensions { get; set; } = new();
    public string TotalSizeDisplay => FileItem.FormatSize(TotalSize);
}

/// <summary>
/// 单个扩展名分组的统计。
/// </summary>
public class ExtensionGroup
{
    public string Extension { get; set; } = "";
    public int Count { get; set; }
    public long TotalSize { get; set; }
    public string TotalSizeDisplay => FileItem.FormatSize(TotalSize);
}
