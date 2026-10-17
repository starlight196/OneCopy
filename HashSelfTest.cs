// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

#if DEBUG
using System.Text;

namespace OneCopy;

/// <summary>
/// 哈希算法自检：用官方测试向量校验 SHA-256 / XXH3-128 / BLAKE3，
/// 并交叉验证「一次性计算」与「分块流式计算」结果一致。
/// 仅在 Debug 构建中可用。
/// </summary>
internal static class HashSelfTest
{
    /// <summary>运行全部自检，返回通过数量与失败描述。</summary>
    public static (int Passed, List<string> Failures) Run()
    {
        int passed = 0;
        var failures = new List<string>();

        void Check(string name, string actual, string expected)
        {
            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                passed++;
            }
            else
            {
                failures.Add($"{name}: expected {expected}, actual {actual}");
            }
        }

        // ===== XXH3-128 官方兼容测试向量（输入 = (i+1) % 251） =====
        foreach ((int length, string expected) in Xxh3Vectors)
        {
            byte[] data = Pattern(length, startAt: 1);
            Check($"xxh3-128[{length}]", FileHasher.Hash(data, HashAlgorithmKind.Xxh3_128), expected);
        }

        // ===== BLAKE3 官方测试向量（输入 = i % 251） =====
        foreach ((int length, string expected) in Blake3Vectors)
        {
            byte[] data = Pattern(length, startAt: 0);
            Check($"blake3[{length}]", FileHasher.Hash(data, HashAlgorithmKind.Blake3), expected);
        }

        // ===== SHA-256 标准向量 =====
        Check("sha256[abc]",
            FileHasher.Hash("abc"u8, HashAlgorithmKind.Sha256),
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

        // ===== 流式 vs 一次性交叉验证 =====
        int[] streamingLengths =
        {
            0, 1, 2, 3, 15, 16, 17, 31, 63, 64, 65, 127, 128, 129, 239, 240, 241, 255,
            256, 257, 300, 511, 512, 1023, 1024, 1025, 2048, 4095, 8191, 12345, 65536,
        };
        foreach (int length in streamingLengths)
        {
            byte[] data = Pattern(length, startAt: 3);

            byte[] xxhOneShot = XxHash3_128.Hash(data);
            byte[] xxhStreaming = StreamXxh3(data);
            Check($"xxh3-128 streaming[{length}]",
                Convert.ToHexString(xxhStreaming).ToLowerInvariant(),
                Convert.ToHexString(xxhOneShot).ToLowerInvariant());

            byte[] blakeOneShot = Blake3Hasher.Hash(data);
            byte[] blakeStreaming = StreamBlake3(data);
            Check($"blake3 streaming[{length}]",
                Convert.ToHexString(blakeStreaming).ToLowerInvariant(),
                Convert.ToHexString(blakeOneShot).ToLowerInvariant());
        }

        return (passed, failures);
    }

    private delegate void SpanAction(ReadOnlySpan<byte> data);

    private static byte[] StreamXxh3(byte[] data)
    {
        var hasher = new XxHash3_128();
        FeedInChunks(data, hasher.Update);
        return hasher.GetHash();
    }

    private static byte[] StreamBlake3(byte[] data)
    {
        var hasher = new Blake3Hasher();
        FeedInChunks(data, hasher.Update);
        return hasher.GetHash();
    }

    private static void FeedInChunks(byte[] data, SpanAction update)
    {
        int offset = 0;
        int i = 0;
        while (offset < data.Length)
        {
            int chunk = (i * 37 + 11) % 997 + 1; // 1..997 的伪随机分块
            int take = Math.Min(chunk, data.Length - offset);
            update(data.AsSpan(offset, take));
            offset += take;
            i++;
        }
    }

    private static byte[] Pattern(int length, int startAt)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (byte)((i + startAt) % 251);
        }
        return data;
    }

    // 输入 = (i+1) % 251，长度即数组下标；期望值为标准 XXH3-128（大端 16 字节）。
    private static readonly (int Length, string Hex)[] Xxh3Vectors =
    {
        (0, "99aa06d3014798d86001c324468d497f"),
        (1, "51025a4491835505e12ef9d2eb86ceeb"),
        (2, "d2f0f9428898845f08130b77ddef5807"),
        (3, "ac77eb88cbc4b8d4ebce9b7632ae733b"),
        (4, "49a04899597a3567537653a0d9955b86"),
        (5, "13d8ee1d6dd32c9c244bd8eab1d14be3"),
        (8, "2ab463fddb09a0b83e8675c57268fb02"),
        (9, "e338e616502be3613c4087b7dea54fc0"),
        (16, "6d84a882f6411b41eada823104bd7174"),
        (17, "9ac14e2c3fe59a83acda8373034d6aaf"),
        (31, "20349822c8a291871580963b82817a62"),
        (32, "6558716845a29a6cd4e2dfc12b4b57ed"),
        (33, "511310aba0443aace0e91dd44e49b7d0"),
        (64, "21bfcfd7d148a3df639fbd9cf9bdfb51"),
        (65, "2f2ef5c0f071aabf839a6f242d0884a1"),
        (96, "2c932ae1bf5ef99ddf5af453c37a4873"),
        (100, "908495a789d76a980dc4a6a00105b3fa"),
        (120, "d7fcf1b8ec7fe37aee70e237e4028c5f"),
        (127, "94090d4e5319f62a789de070f14591ce"),
        (128, "763fdbd9fc6022337ac9e58028da0fc7"),
        (129, "4c8ce7bd2a6024b388cb4305c9a32490"),
        (130, "cc46645bf62bdda977065b2b723027b3"),
        (160, "113b1d66f56cdd354686859c80eb768c"),
        (192, "4f7d724c22482f733afbb6143da36c4b"),
        (200, "4d5d2c739a03aef0af7e5fb544c9ec7a"),
        (224, "baa9123db076cc666178f26d8b606d83"),
        (239, "8250ad9a43eff11bfcb0da8465ed3e56"),
        (240, "2007e6f83d506ea3dde80e1ba2971e09"),
        (241, "956bc01534a3752bb6515f490cdd4ce5"),
        (255, "269f1204c051f08efce9bbd7cb663ed8"),
        (256, "00cf4bad01f010bd6c602b93da37d220"),
        (257, "60e895e5bffb924961a47969771c1a22"),
        (300, "3f17167946cad9dc82f60d0140734098"),
        (350, "39187d560d567818e57fb5632bfff8b8"),
        (390, "55803b2fe3f1c6bdb49921c09190f913"),
    };

    // 输入 = i % 251（BLAKE3 官方测试向量的输入模式），输出前 32 字节。
    private static readonly (int Length, string Hex)[] Blake3Vectors =
    {
        (0, "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262"),
        (1, "2d3adedff11b61f14c886e35afa036736dcd87a74d27b5c1510225d0f592e213"),
        (2, "7b7015bb92cf0b318037702a6cdd81dee41224f734684c2c122cd6359cb1ee63"),
        (3, "e1be4d7a8ab5560aa4199eea339849ba8e293d55ca0a81006726d184519e647f"),
        (4, "f30f5ab28fe047904037f77b6da4fea1e27241c5d132638d8bedce9d40494f32"),
        (5, "b40b44dfd97e7a84a996a91af8b85188c66c126940ba7aad2e7ae6b385402aa2"),
        (6, "06c4e8ffb6872fad96f9aaca5eee1553eb62aed0ad7198cef42e87f6a616c844"),
        (7, "3f8770f387faad08faa9d8414e9f449ac68e6ff0417f673f602a646a891419fe"),
        (8, "2351207d04fc16ade43ccab08600939c7c1fa70a5c0aaca76063d04c3228eaeb"),
        (63, "e9bc37a594daad83be9470df7f7b3798297c3d834ce80ba85d6e207627b7db7b"),
        (64, "4eed7141ea4a5cd4b788606bd23f46e212af9cacebacdc7d1f4c6dc7f2511b98"),
        (65, "de1e5fa0be70df6d2be8fffd0e99ceaa8eb6e8c93a63f2d8d1c30ecb6b263dee"),
        (127, "d81293fda863f008c09e92fc382a81f5a0b4a1251cba1634016a0f86a6bd640d"),
        (128, "f17e570564b26578c33bb7f44643f539624b05df1a76c81f30acd548c44b45ef"),
        (129, "683aaae9f3c5ba37eaaf072aed0f9e30bac0865137bae68b1fde4ca2aebdcb12"),
        (1023, "10108970eeda3eb932baac1428c7a2163b0e924c9a9e25b35bba72b28f70bd11"),
        (1024, "42214739f095a406f3fc83deb889744ac00df831c10daa55189b5d121c855af7"),
        (1025, "d00278ae47eb27b34faecf67b4fe263f82d5412916c1ffd97c8cb7fb814b8444"),
        (2048, "e776b6028c7cd22a4d0ba182a8bf62205d2ef576467e838ed6f2529b85fba24a"),
        (2049, "5f4d72f40d7a5f82b15ca2b2e44b1de3c2ef86c426c95c1af0b6879522563030"),
        (3072, "b98cb0ff3623be03326b373de6b9095218513e64f1ee2edd2525c7ad1e5cffd2"),
        (4096, "015094013f57a5277b59d8475c0501042c0b642e531b0a1c8f58d2163229e969"),
        (5120, "9cadc15fed8b5d854562b26a9536d9707cadeda9b143978f319ab34230535833"),
        (16384, "f875d6646de28985646f34ee13be9a576fd515f76b5b0a26bb324735041ddde4"),
        (102400, "bc3e3d41a1146b069abffad3c0d44860cf664390afce4d9661f7902e7943e085"),
    };

    /// <summary>把自检结果写入文本行，供 smoke 使用。</summary>
    public static void AppendTo(List<string> lines)
    {
        (int passed, List<string> failures) = Run();
        lines.Add($"hashSelfTest passed={passed} failed={failures.Count}");
        foreach (string failure in failures.Take(10))
        {
            lines.Add("hashSelfTest-fail " + failure);
        }
    }

    /// <summary>控制台哈希自检入口（--hash-test）。</summary>
    public static int RunConsole(string logPath)
    {
        (int passed, List<string> failures) = Run();
        var lines = new List<string> { $"hash tests: passed={passed} failed={failures.Count}" };
        lines.AddRange(failures.Select(f => "FAIL " + f));
        File.WriteAllLines(logPath, lines, Encoding.UTF8);
        Console.WriteLine(lines[0]);
        foreach (string failure in failures.Take(30))
        {
            Console.WriteLine("FAIL " + failure);
        }
        return failures.Count == 0 ? 0 : 1;
    }
}
#endif
