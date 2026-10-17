// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneCopy;

/// <summary>界面主题模式。</summary>
internal enum ThemeMode
{
    Light,
    Dark,
    System,
    Custom,
}

/// <summary>
/// 应用设置：外观模式（含自定义深色时段）与全局排除扩展名。
/// 保存为 %AppData%\OneCopy\settings.json；读写失败一律退回默认值，不影响主流程。
/// </summary>
internal sealed class AppSettings
{
    private const string DefaultDarkStart = "19:00";
    private const string DefaultDarkEnd = "07:00";
    private static readonly TimeOnly FallbackDarkStart = new(19, 0);
    private static readonly TimeOnly FallbackDarkEnd = new(7, 0);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>设置结构版本，便于以后迁移。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>外观模式。</summary>
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    /// <summary>自定义模式下深色时段的开始时间（24 小时制，HH:mm）。</summary>
    public string DarkStart { get; set; } = DefaultDarkStart;

    /// <summary>自定义模式下深色时段的结束时间（24 小时制，HH:mm）。</summary>
    public string DarkEnd { get; set; } = DefaultDarkEnd;

    /// <summary>全局排除的扩展名（含前导点，例如 .tmp）。</summary>
    public List<string> ExcludedExtensions { get; set; } = new();

    /// <summary>测试用：覆盖设置文件路径；为空时使用 %AppData%\OneCopy\settings.json。</summary>
    internal static string? PathOverride { get; set; }

    /// <summary>当前设置文件路径。</summary>
    public static string SettingsPath => string.IsNullOrWhiteSpace(PathOverride)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OneCopy",
            "settings.json")
        : PathOverride!;

    /// <summary>把排除扩展名转换成扫描器使用的大小写不敏感集合。</summary>
    public HashSet<string> BuildExcludedSet() =>
        new(ExcludedExtensions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

    public TimeOnly GetDarkStart() => ParseTime(DarkStart, FallbackDarkStart);

    public TimeOnly GetDarkEnd() => ParseTime(DarkEnd, FallbackDarkEnd);

    public void SetDarkRange(TimeOnly start, TimeOnly end)
    {
        DarkStart = FormatTime(start);
        DarkEnd = FormatTime(end);
    }

    /// <summary>设置的自定义深色时段文本，例如 "19:00 - 07:00"。</summary>
    public string DarkRangeDisplay => $"{FormatTime(GetDarkStart())} - {FormatTime(GetDarkEnd())}";

    /// <summary>按当前设置计算某一时刻应使用的主题（true = 深色）。</summary>
    public bool ResolveDark(DateTime now)
    {
        switch (ThemeMode)
        {
            case ThemeMode.Light:
                return false;

            case ThemeMode.Dark:
                return true;

            case ThemeMode.Custom:
                TimeOnly start = GetDarkStart();
                TimeOnly end = GetDarkEnd();
                TimeOnly time = TimeOnly.FromDateTime(now);
                if (start == end) return true; // 开始与结束相同表示全天深色
                return start < end
                    ? time >= start && time < end
                    : time >= start || time < end; // 跨午夜

            case ThemeMode.System:
            default:
                return Theme.ReadSystemDarkMode();
        }
    }

    public AppSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        ThemeMode = ThemeMode,
        DarkStart = DarkStart,
        DarkEnd = DarkEnd,
        ExcludedExtensions = new List<string>(ExcludedExtensions ?? new List<string>()),
    };

    /// <summary>用另一份设置覆盖当前实例（保留对象引用，界面字段无须重新绑定）。</summary>
    public void CopyFrom(AppSettings other)
    {
        SchemaVersion = other.SchemaVersion;
        ThemeMode = other.ThemeMode;
        DarkStart = other.DarkStart;
        DarkEnd = other.DarkEnd;
        ExcludedExtensions = new List<string>(other.ExcludedExtensions ?? new List<string>());
    }

    // ===== 持久化 =====

    /// <summary>读取设置；文件不存在或内容损坏时返回默认设置。</summary>
    public static AppSettings Load()
    {
        try
        {
            string path = SettingsPath;
            if (!File.Exists(path)) return new AppSettings();

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return new AppSettings();

            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null) return new AppSettings();

            settings.Sanitize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>保存设置；失败时静默返回 false（无写权限等情况下仍可正常使用程序）。</summary>
    public bool Save()
    {
        try
        {
            string path = SettingsPath;
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(this, JsonOptions);
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Sanitize()
    {
        if (!Enum.IsDefined(ThemeMode)) ThemeMode = ThemeMode.System;
        if (!TryParseTime(DarkStart, out _)) DarkStart = DefaultDarkStart;
        if (!TryParseTime(DarkEnd, out _)) DarkEnd = DefaultDarkEnd;
        DarkStart = FormatTime(GetDarkStart());
        DarkEnd = FormatTime(GetDarkEnd());
        ExcludedExtensions = NormalizeExtensions(ExcludedExtensions, out _);
    }

    // ===== 扩展名解析 =====

    /// <summary>把多行文本解析为规范化扩展名列表，同时返回无法识别的条目。</summary>
    public static List<string> ParseExtensionsText(string text, out List<string> invalid) =>
        NormalizeExtensions(SplitExtensionTokens(text), out invalid);

    /// <summary>规范化扩展名：去空白、补前导点、忽略 "*.xxx" 写法、小写、去重排序。</summary>
    public static List<string> NormalizeExtensions(IEnumerable<string>? raw, out List<string> invalid)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        invalid = new List<string>();

        foreach (string item in raw ?? Enumerable.Empty<string>())
        {
            string original = (item ?? string.Empty).Trim();
            if (original.Length == 0) continue;

            string token = original.Trim('*').Trim();
            if (token.Length == 0) continue;
            if (!token.StartsWith('.')) token = "." + token;
            token = token.ToLowerInvariant();

            if (!IsValidExtension(token))
            {
                invalid.Add(original);
                continue;
            }

            if (seen.Add(token)) result.Add(token);
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static IEnumerable<string> SplitExtensionTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return text.Split(
            new[] { "\r\n", "\n", "\r", ",", ";", " ", "\t" },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsValidExtension(string token)
    {
        // token 形如 ".tmp"：至少 2 个字符，且只允许常见的扩展名字符
        if (token.Length < 2 || token[0] != '.') return false;

        for (int i = 1; i < token.Length; i++)
        {
            char c = token[i];
            bool allowed = char.IsLetterOrDigit(c) || c is '_' or '-' or '+' or '@' or '#' or '~' or '$';
            if (!allowed) return false;
        }
        return true;
    }

    // ===== 时间解析 =====

    private static readonly string[] TimeFormats = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" };

    private static bool TryParseTime(string? text, out TimeOnly value) =>
        TimeOnly.TryParseExact(
            (text ?? string.Empty).Trim(),
            TimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out value);

    private static TimeOnly ParseTime(string? text, TimeOnly fallback) =>
        TryParseTime(text, out TimeOnly value) ? value : fallback;

    private static string FormatTime(TimeOnly value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);
}
