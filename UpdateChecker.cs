// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OneCopy;

/// <summary>检查更新的结果状态。</summary>
internal enum UpdateCheckState
{
    UpToDate,
    Newer,
    NoRelease,
    Failed,
}

/// <summary>检查更新的结果。</summary>
internal sealed class UpdateCheckResult
{
    public UpdateCheckState State { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? LatestVersion { get; init; }
    public string? DownloadUrl { get; init; }

    public static UpdateCheckResult UpToDate(string version) => new()
    {
        State = UpdateCheckState.UpToDate,
        Message = $"已是最新版本（v{version}）。",
        LatestVersion = version,
    };

    public static UpdateCheckResult Newer(string version, string url) => new()
    {
        State = UpdateCheckState.Newer,
        Message = $"发现新版本 v{version}，可打开发布页下载。",
        LatestVersion = version,
        DownloadUrl = url,
    };

    public static UpdateCheckResult NoRelease(string message) => new()
    {
        State = UpdateCheckState.NoRelease,
        Message = message,
    };

    public static UpdateCheckResult Failed(string message) => new()
    {
        State = UpdateCheckState.Failed,
        Message = message,
    };
}

/// <summary>
/// 检查更新：读取 GitHub 上最新的 Release 版本号并与当前程序版本比较。
/// 网络异常、仓库未发布版本等情况都会返回可读的结果，不抛异常给界面。
/// </summary>
internal static class UpdateChecker
{
    public const string RepositoryUrl = "https://github.com/starlight196/OneCopy";
    public const string ReleasesUrl = RepositoryUrl + "/releases";

    private const string LatestReleaseApi = "https://api.github.com/repos/starlight196/OneCopy/releases/latest";

    /// <summary>当前程序版本（去掉 "+提交哈希" 后缀）。</summary>
    public static string CurrentVersion
    {
        get
        {
            string version = (Application.ProductVersion ?? "0.0.0").Trim();
            int plus = version.IndexOf('+');
            if (plus >= 0) version = version[..plus];
            return version;
        }
    }

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OneCopy/" + CurrentVersion);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using HttpResponseMessage response = await client
                .GetAsync(LatestReleaseApi, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UpdateCheckResult.NoRelease("GitHub 上还没有发布版本，可打开发布页手动查看。");
            }
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed($"更新服务器返回 HTTP {(int)response.StatusCode}，请稍后再试。");
            }

            // 防御性检查：正常的 Release JSON 只有几 KB，过大的响应直接拒绝，避免被异常数据拖垮
            if (response.Content.Headers.ContentLength is long contentLength && contentLength > 1024 * 1024)
            {
                return UpdateCheckResult.Failed("更新服务器返回的数据异常，请稍后再试。");
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string tag = root.TryGetProperty("tag_name", out JsonElement tagElement)
                ? tagElement.GetString() ?? string.Empty
                : string.Empty;
            string url = root.TryGetProperty("html_url", out JsonElement urlElement)
                ? urlElement.GetString() ?? ReleasesUrl
                : ReleasesUrl;
            if (!IsAllowedUrl(url)) url = ReleasesUrl;

            Version? latest = ParseVersion(tag);
            Version? current = ParseVersion(CurrentVersion);
            if (latest is null || current is null)
            {
                return UpdateCheckResult.Failed("无法解析版本信息。");
            }

            if (latest <= current) return UpdateCheckResult.UpToDate(Format(current));
            return UpdateCheckResult.Newer(Format(latest), url);
        }
        catch (OperationCanceledException)
        {
            return UpdateCheckResult.Failed("检查更新超时，请稍后重试。");
        }
        catch (HttpRequestException)
        {
            return UpdateCheckResult.Failed("无法连接更新服务器，请检查网络后重试。");
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed("检查更新失败：" + ex.Message);
        }
    }

    /// <summary>候选版本是否比当前版本新。</summary>
    internal static bool IsNewer(string? candidate, string? current)
    {
        Version? left = ParseVersion(candidate);
        Version? right = ParseVersion(current);
        return left is not null && right is not null && left > right;
    }

    /// <summary>
    /// 只允许打开 GitHub 的 http/https 链接。
    /// 链接来自网络响应，即使被篡改也只会被忽略，不会被 UseShellExecute 拉起本地的 exe / 脚本；
    /// file:、javascript:、本地路径等一律拒绝。
    /// </summary>
    internal static bool IsAllowedUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

        string host = uri.Host;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>把 "v1.3.0"、"1.3"、"1.3.0+abc" 等文本解析为 Version。</summary>
    internal static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
        int plus = value.IndexOf('+');
        if (plus >= 0) value = value[..plus];

        return Version.TryParse(value, out Version? version) ? version : null;
    }

    private static string Format(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
}
