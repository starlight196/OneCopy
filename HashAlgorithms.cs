// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;

namespace OneCopy;

/// <summary>可选的哈希算法。</summary>
public enum HashAlgorithmKind
{
    /// <summary>SHA-256（默认，加密级强度）。</summary>
    Sha256,

    /// <summary>XXH3-128（非加密校验，速度极快）。</summary>
    Xxh3_128,

    /// <summary>BLAKE3（加密级强度，速度快）。</summary>
    Blake3,
}

/// <summary>哈希算法的显示信息。</summary>
internal static class HashAlgorithmInfo
{
    public static readonly HashAlgorithmKind[] All =
    {
        HashAlgorithmKind.Sha256,
        HashAlgorithmKind.Xxh3_128,
        HashAlgorithmKind.Blake3,
    };

    public static string DisplayName(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Xxh3_128 => "XXH3-128",
        HashAlgorithmKind.Blake3 => "BLAKE3",
        _ => "SHA-256",
    };

    public static string Description(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Xxh3_128 => "非加密校验，速度最快，适合大文件夹。",
        HashAlgorithmKind.Blake3 => "加密级强度，速度比 SHA-256 快很多。",
        _ => "最通用的加密级哈希，兼容性最好（推荐默认）。",
    };
}

/// <summary>
/// 文件哈希计算：按设置选择 SHA-256 / XXH3-128 / BLAKE3，返回小写十六进制字符串。
/// 全部实现都使用流式读取，大文件不会一次性载入内存。
/// </summary>
internal static class FileHasher
{
    private const int ReadBufferSize = 1_048_576;

    /// <summary>计算文件的哈希（小写十六进制）。</summary>
    public static string HashFile(string filePath, HashAlgorithmKind algorithm)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);

        return HashStream(stream, algorithm);
    }

    /// <summary>计算内存数据的哈希（小写十六进制）。</summary>
    public static string Hash(ReadOnlySpan<byte> data, HashAlgorithmKind algorithm) => algorithm switch
    {
        HashAlgorithmKind.Xxh3_128 => ToHex(XxHash3_128.Hash(data)),
        HashAlgorithmKind.Blake3 => ToHex(Blake3Hasher.Hash(data)),
        _ => ToHex(SHA256.HashData(data)),
    };

    private static string HashStream(Stream stream, HashAlgorithmKind algorithm)
    {
        switch (algorithm)
        {
            case HashAlgorithmKind.Xxh3_128:
            {
                var hasher = new XxHash3_128();
                byte[] buffer = new byte[ReadBufferSize];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hasher.Update(buffer.AsSpan(0, read));
                }
                return ToHex(hasher.GetHash());
            }

            case HashAlgorithmKind.Blake3:
            {
                var hasher = new Blake3Hasher();
                byte[] buffer = new byte[ReadBufferSize];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hasher.Update(buffer.AsSpan(0, read));
                }
                return ToHex(hasher.GetHash());
            }

            default:
            {
                using var sha256 = SHA256.Create();
                return ToHex(sha256.ComputeHash(stream));
            }
        }
    }

    private static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}
