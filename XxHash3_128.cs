// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace OneCopy;

/// <summary>
/// XXH3-128（xxHash 128 位）的可移植实现，用于重复文件查找的快速校验。
/// 算法与参考实现保持一致，输出为标准 16 字节大端格式（高 64 位在前），
/// 与 xxhsum / System.IO.Hashing.XxHash128 的输出相同。
/// 参考：https://github.com/Cyan4973/xxHash （BSD-2-Clause）
/// </summary>
internal sealed class XxHash3_128
{
    public const int HashSizeInBytes = 16;

    private const int StripeLengthBytes = 64;
    private const int SecretLengthBytes = 192;
    private const int SecretConsumeRateBytes = 8;
    private const int SecretMergeAccsStartBytes = 11;
    private const int SecretLastAccStartBytes = 7;
    private const int AccumulatorCount = 8;
    private const int NumStripesPerBlock = (SecretLengthBytes - StripeLengthBytes) / SecretConsumeRateBytes; // 16
    private const int InternalBufferLengthBytes = 256;
    private const int InternalBufferStripes = InternalBufferLengthBytes / StripeLengthBytes; // 4
    private const int MidSizeMaxBytes = 240;

    private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime64_3 = 0x165667B19E3779F9UL;
    private const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime64_5 = 0x27D4EB2F165667C5UL;
    private const uint Prime32_1 = 0x9E3779B1U;
    private const uint Prime32_2 = 0x85EBCA77U;
    private const uint Prime32_3 = 0xC2B2AE3DU;

    private static readonly byte[] DefaultSecret =
    {
        0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe,
        0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c,
        0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb,
        0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f,
        0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78,
        0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21,
        0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e,
        0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c,
        0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb,
        0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3,
        0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e,
        0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8,
        0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f,
        0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d,
        0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31,
        0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64,
        0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3,
        0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb,
        0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49,
        0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e,
        0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc,
        0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce,
        0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28,
        0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e,
    };

    private const ulong S0 = 0xBE4BA423396CFEB8UL;
    private const ulong S1 = 0x1CAD21F72C81017CUL;
    private const ulong S2 = 0xDB979083E96DD4DEUL;
    private const ulong S3 = 0x1F67B3B7A4A44072UL;
    private const ulong S4 = 0x78E5C0CC4EE679CBUL;
    private const ulong S5 = 0x2172FFCC7DD05A82UL;
    private const ulong S6 = 0x8E2443F7744608B8UL;
    private const ulong S7 = 0x4C263A81E69035E0UL;
    private const ulong S8 = 0xCB00C391BB52283CUL;
    private const ulong S9 = 0xA32E531B8B65D088UL;
    private const ulong S10 = 0x4EF90DA297486471UL;
    private const ulong S11 = 0xD8ACDEA946EF1938UL;
    private const ulong S12 = 0x3F349CE33F76FAA8UL;
    private const ulong S13 = 0x1D4F0BC7C7BBDCF9UL;
    private const ulong S14 = 0x3159B4CD4BE0518AUL;
    private const ulong S15 = 0x647378D9C97E9FC8UL;

    private const ulong S3_0 = 0x81017CBE4BA42339UL;
    private const ulong S3_1 = 0x6DD4DE1CAD21F72CUL;
    private const ulong S3_2 = 0xA44072DB979083E9UL;
    private const ulong S3_3 = 0xE679CB1F67B3B7A4UL;
    private const ulong S3_4 = 0xD05A8278E5C0CC4EUL;
    private const ulong S3_5 = 0x4608B82172FFCC7DUL;
    private const ulong S3_6 = 0x9035E08E2443F774UL;
    private const ulong S3_7 = 0x52283C4C263A81E6UL;
    private const ulong S3_8 = 0x65D088CB00C391BBUL;
    private const ulong S3_9 = 0x486471A32E531B8BUL;
    private const ulong S3_10 = 0xEF19384EF90DA297UL;
    private const ulong S3_11 = 0x76FAA8D8ACDEA946UL;

    // ===== 流式状态 =====

    private readonly ulong[] _accumulators = new ulong[AccumulatorCount];
    private readonly byte[] _buffer = new byte[InternalBufferLengthBytes];
    private uint _bufferedCount;
    private ulong _stripesProcessedInCurrentBlock;
    private ulong _totalLength;

    public XxHash3_128() => Reset();

    public void Reset()
    {
        _bufferedCount = 0;
        _stripesProcessedInCurrentBlock = 0;
        _totalLength = 0;
        InitializeAccumulators(_accumulators);
        Array.Clear(_buffer);
    }

    public void Update(ReadOnlySpan<byte> source)
    {
        _totalLength += (uint)source.Length;

        // 小输入：先放进缓冲
        if (source.Length <= InternalBufferLengthBytes - (int)_bufferedCount)
        {
            source.CopyTo(_buffer.AsSpan((int)_bufferedCount));
            _bufferedCount += (uint)source.Length;
            return;
        }

        int sourceIndex = 0;
        if (_bufferedCount != 0)
        {
            int loadSize = InternalBufferLengthBytes - (int)_bufferedCount;
            source[..loadSize].CopyTo(_buffer.AsSpan((int)_bufferedCount, loadSize));
            sourceIndex = loadSize;

            ConsumeStripes(_accumulators, ref _stripesProcessedInCurrentBlock,
                NumStripesPerBlock, _buffer, InternalBufferStripes, 0);
            _bufferedCount = 0;
        }

        if (source.Length - sourceIndex > NumStripesPerBlock * StripeLengthBytes)
        {
            ulong stripes = (ulong)(source.Length - sourceIndex - 1) / StripeLengthBytes;

            // 补齐当前块
            ulong stripesToEnd = NumStripesPerBlock - _stripesProcessedInCurrentBlock;
            Accumulate(_accumulators, source[sourceIndex..], 0, (int)_stripesProcessedInCurrentBlock * SecretConsumeRateBytes, (int)stripesToEnd);
            ScrambleAccumulators(_accumulators);
            _stripesProcessedInCurrentBlock = 0;
            sourceIndex += (int)stripesToEnd * StripeLengthBytes;
            stripes -= stripesToEnd;

            // 整块处理
            while (stripes >= NumStripesPerBlock)
            {
                Accumulate(_accumulators, source[sourceIndex..], 0, 0, NumStripesPerBlock);
                ScrambleAccumulators(_accumulators);
                sourceIndex += NumStripesPerBlock * StripeLengthBytes;
                stripes -= NumStripesPerBlock;
            }

            // 处理最后不足一块的完整 stripe
            Accumulate(_accumulators, source[sourceIndex..], 0, 0, (int)stripes);
            sourceIndex += (int)stripes * StripeLengthBytes;
            _stripesProcessedInCurrentBlock = stripes;

            // 保留最后一个 stripe 供收尾使用
            source.Slice(sourceIndex - StripeLengthBytes, StripeLengthBytes)
                .CopyTo(_buffer.AsSpan(InternalBufferLengthBytes - StripeLengthBytes));
        }
        else if (source.Length - sourceIndex > InternalBufferLengthBytes)
        {
            do
            {
                ConsumeStripes(_accumulators, ref _stripesProcessedInCurrentBlock,
                    NumStripesPerBlock, source[sourceIndex..], InternalBufferStripes, 0);
                sourceIndex += InternalBufferLengthBytes;
            }
            while (source.Length - sourceIndex > InternalBufferLengthBytes);

            source.Slice(sourceIndex - StripeLengthBytes, StripeLengthBytes)
                .CopyTo(_buffer.AsSpan(InternalBufferLengthBytes - StripeLengthBytes));
        }

        int remaining = source.Length - sourceIndex;
        source[sourceIndex..].CopyTo(_buffer);
        _bufferedCount = (uint)remaining;
    }

    /// <summary>输出标准 16 字节哈希（高 64 位在前，大端）。</summary>
    public byte[] GetHash()
    {
        ulong low;
        ulong high;

        if (_totalLength > MidSizeMaxBytes)
        {
            var acc = new ulong[AccumulatorCount];
            Array.Copy(_accumulators, acc, AccumulatorCount);
            DigestLong(acc);

            low = MergeAccumulators(acc, SecretMergeAccsStartBytes, _totalLength * Prime64_1);
            high = MergeAccumulators(
                acc,
                SecretLengthBytes - AccumulatorCount * sizeof(ulong) - SecretMergeAccsStartBytes,
                ~(_totalLength * Prime64_2));
        }
        else
        {
            var data = new ReadOnlySpan<byte>(_buffer, 0, (int)_totalLength);
            (low, high) = HashShort(data);
        }

        byte[] result = new byte[HashSizeInBytes];
        BinaryPrimitives.WriteUInt64BigEndian(result, high);
        BinaryPrimitives.WriteUInt64BigEndian(result.AsSpan(8), low);
        return result;
    }

    /// <summary>一次性计算 XXH3-128。</summary>
    public static byte[] Hash(ReadOnlySpan<byte> source)
    {
        ulong low;
        ulong high;

        if (source.Length <= MidSizeMaxBytes)
        {
            (low, high) = HashShort(source);
        }
        else
        {
            var acc = new ulong[AccumulatorCount];
            InitializeAccumulators(acc);
            HashLong(source, acc);

            low = MergeAccumulators(acc, SecretMergeAccsStartBytes, (ulong)source.Length * Prime64_1);
            high = MergeAccumulators(
                acc,
                SecretLengthBytes - AccumulatorCount * sizeof(ulong) - SecretMergeAccsStartBytes,
                ~((ulong)source.Length * Prime64_2));
        }

        byte[] result = new byte[HashSizeInBytes];
        BinaryPrimitives.WriteUInt64BigEndian(result, high);
        BinaryPrimitives.WriteUInt64BigEndian(result.AsSpan(8), low);
        return result;
    }

    // ===== 短输入（<= 240 字节，一次性路径） =====

    private static (ulong Low, ulong High) HashShort(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        if (length <= 16) return HashLength0To16(source);
        if (length <= 128) return HashLength17To128(source);
        return HashLength129To240(source);
    }

    private static (ulong Low, ulong High) HashLength0To16(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        if (length > 8) return HashLength9To16(source);
        if (length >= 4) return HashLength4To8(source);
        if (length != 0) return HashLength1To3(source);

        ulong bitFlipL = S8 ^ S9;
        ulong bitFlipH = S10 ^ S11;
        return (Avalanche64(bitFlipL), Avalanche64(bitFlipH));
    }

    private static (ulong Low, ulong High) HashLength1To3(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        byte c1 = source[0];
        byte c2 = source[length >> 1];
        byte c3 = source[length - 1];

        uint combinedl = ((uint)c1 << 16) | ((uint)c2 << 24) | c3 | ((uint)length << 8);
        uint combinedh = System.Numerics.BitOperations.RotateLeft(BinaryPrimitives.ReverseEndianness(combinedl), 13);

        const uint secretXorL = unchecked((uint)S0) ^ unchecked((uint)(S0 >> 32));
        const uint secretXorH = unchecked((uint)S1) ^ unchecked((uint)(S1 >> 32));
        ulong keyedLo = combinedl ^ secretXorL;
        ulong keyedHi = combinedh ^ secretXorH;

        return (Avalanche64(keyedLo), Avalanche64(keyedHi));
    }

    private static (ulong Low, ulong High) HashLength4To8(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        uint inputLo = BinaryPrimitives.ReadUInt32LittleEndian(source);
        uint inputHi = BinaryPrimitives.ReadUInt32LittleEndian(source[(length - 4)..]);
        ulong input64 = inputLo + ((ulong)inputHi << 32);
        ulong bitflip = S2 ^ S3;
        ulong keyed = input64 ^ bitflip;

        ulong m128High = Multiply64To128(keyed, Prime64_1 + ((ulong)length << 2), out ulong m128Low);

        m128High += m128Low << 1;
        m128Low ^= m128High >> 3;

        m128Low = XorShift(m128Low, 35);
        m128Low *= 0x9FB21C651E98DF25UL;
        m128Low = XorShift(m128Low, 28);
        m128High = Avalanche(m128High);

        return (m128Low, m128High);
    }

    private static (ulong Low, ulong High) HashLength9To16(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        ulong bitflipl = S4 ^ S5;
        ulong bitfliph = S6 ^ S7;
        ulong inputLo = BinaryPrimitives.ReadUInt64LittleEndian(source);
        ulong inputHi = BinaryPrimitives.ReadUInt64LittleEndian(source[(length - 8)..]);
        ulong m128High = Multiply64To128(inputLo ^ inputHi ^ bitflipl, Prime64_1, out ulong m128Low);

        m128Low += (ulong)(length - 1) << 54;
        inputHi ^= bitfliph;

        m128High += inputHi + Multiply32To64((uint)inputHi, Prime32_2 - 1);
        m128Low ^= BinaryPrimitives.ReverseEndianness(m128High);

        ulong h128High = Multiply64To128(m128Low, Prime64_2, out ulong h128Low);
        h128High += m128High * Prime64_2;

        h128Low = Avalanche(h128Low);
        h128High = Avalanche(h128High);
        return (h128Low, h128High);
    }

    private static (ulong Low, ulong High) HashLength17To128(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        ulong accLow = (ulong)length * Prime64_1;
        ulong accHigh = 0;

        switch ((length - 1) / 32)
        {
            default:
                Mix32Bytes(ref accLow, ref accHigh, source[48..], source[(length - 64)..], S12, S13, S14, S15);
                goto case 2;
            case 2:
                Mix32Bytes(ref accLow, ref accHigh, source[32..], source[(length - 48)..], S8, S9, S10, S11);
                goto case 1;
            case 1:
                Mix32Bytes(ref accLow, ref accHigh, source[16..], source[(length - 32)..], S4, S5, S6, S7);
                goto case 0;
            case 0:
                Mix32Bytes(ref accLow, ref accHigh, source, source[(length - 16)..], S0, S1, S2, S3);
                break;
        }

        return AvalancheHash(accLow, accHigh, (ulong)length);
    }

    private static (ulong Low, ulong High) HashLength129To240(ReadOnlySpan<byte> source)
    {
        int length = source.Length;
        ulong accLow = (ulong)length * Prime64_1;
        ulong accHigh = 0;

        Mix32Bytes(ref accLow, ref accHigh, source, source[16..], S0, S1, S2, S3);
        Mix32Bytes(ref accLow, ref accHigh, source[32..], source[48..], S4, S5, S6, S7);
        Mix32Bytes(ref accLow, ref accHigh, source[64..], source[80..], S8, S9, S10, S11);
        Mix32Bytes(ref accLow, ref accHigh, source[96..], source[112..], S12, S13, S14, S15);

        accLow = Avalanche(accLow);
        accHigh = Avalanche(accHigh);

        int bound = (length - (32 * 4)) / 32;
        if (bound != 0)
        {
            Mix32Bytes(ref accLow, ref accHigh, source[128..], source[144..], S3_0, S3_1, S3_2, S3_3);
            if (bound >= 2)
            {
                Mix32Bytes(ref accLow, ref accHigh, source[160..], source[176..], S3_4, S3_5, S3_6, S3_7);
                if (bound == 3)
                {
                    Mix32Bytes(ref accLow, ref accHigh, source[192..], source[208..], S3_8, S3_9, S3_10, S3_11);
                }
            }
        }

        Mix32Bytes(
            ref accLow, ref accHigh,
            source[(length - 16)..], source[(length - 32)..],
            0x4F0BC7C7BBDCF93FUL, 0x59B4CD4BE0518A1DUL, 0x7378D9C97E9FC831UL, 0xEBD33483ACC5EA64UL);

        return AvalancheHash(accLow, accHigh, (ulong)length);
    }

    private static void Mix32Bytes(
        ref ulong accLow, ref ulong accHigh,
        ReadOnlySpan<byte> input1, ReadOnlySpan<byte> input2,
        ulong secret1, ulong secret2, ulong secret3, ulong secret4)
    {
        accLow += Mix16Bytes(input1, secret1, secret2);
        accLow ^= BinaryPrimitives.ReadUInt64LittleEndian(input2) +
                  BinaryPrimitives.ReadUInt64LittleEndian(input2[8..]);
        accHigh += Mix16Bytes(input2, secret3, secret4);
        accHigh ^= BinaryPrimitives.ReadUInt64LittleEndian(input1) +
                   BinaryPrimitives.ReadUInt64LittleEndian(input1[8..]);
    }

    private static ulong Mix16Bytes(ReadOnlySpan<byte> source, ulong secretLow, ulong secretHigh) =>
        Multiply64To128ThenFold(
            BinaryPrimitives.ReadUInt64LittleEndian(source) ^ secretLow,
            BinaryPrimitives.ReadUInt64LittleEndian(source[8..]) ^ secretHigh);

    private static (ulong Low, ulong High) AvalancheHash(ulong accLow, ulong accHigh, ulong length)
    {
        ulong h128Low = accLow + accHigh;
        ulong h128High = (accLow * Prime64_1) + (accHigh * Prime64_4) + (length * Prime64_2);
        h128Low = Avalanche(h128Low);
        h128High = 0UL - Avalanche(h128High);
        return (h128Low, h128High);
    }

    // ===== 长输入（> 240 字节） =====

    private static void HashLong(ReadOnlySpan<byte> source, ulong[] acc)
    {
        int length = source.Length;
        int blocks = (length - 1) / (StripeLengthBytes * NumStripesPerBlock);
        int offset = 0;

        for (int b = 0; b < blocks; b++)
        {
            for (int i = 0; i < NumStripesPerBlock; i++)
            {
                Accumulate512(acc, source[offset..], i * SecretConsumeRateBytes);
                offset += StripeLengthBytes;
            }
            ScrambleAccumulators(acc);
        }

        int stripes = (length - 1 - offset) / StripeLengthBytes;
        for (int i = 0; i < stripes; i++)
        {
            Accumulate512(acc, source[offset..], i * SecretConsumeRateBytes);
            offset += StripeLengthBytes;
        }

        Accumulate512(acc, source[(length - StripeLengthBytes)..],
            SecretLengthBytes - StripeLengthBytes - SecretLastAccStartBytes);
    }

    private void DigestLong(ulong[] acc)
    {
        byte[] lastStripe = new byte[StripeLengthBytes];
        ReadOnlySpan<byte> accumulateData;

        if (_bufferedCount >= StripeLengthBytes)
        {
            uint stripes = (_bufferedCount - 1) / StripeLengthBytes;
            ulong stripesSoFar = _stripesProcessedInCurrentBlock;
            ConsumeStripes(acc, ref stripesSoFar, NumStripesPerBlock, _buffer, stripes, 0);
            accumulateData = _buffer.AsSpan((int)_bufferedCount - StripeLengthBytes);
        }
        else
        {
            int catchupSize = StripeLengthBytes - (int)_bufferedCount;
            _buffer.AsSpan(InternalBufferLengthBytes - catchupSize, catchupSize).CopyTo(lastStripe);
            _buffer.AsSpan(0, (int)_bufferedCount).CopyTo(lastStripe.AsSpan(catchupSize));
            accumulateData = lastStripe;
        }

        Accumulate512(acc, accumulateData,
            SecretLengthBytes - StripeLengthBytes - SecretLastAccStartBytes);
    }

    private static void InitializeAccumulators(ulong[] acc)
    {
        acc[0] = Prime32_3;
        acc[1] = Prime64_1;
        acc[2] = Prime64_2;
        acc[3] = Prime64_3;
        acc[4] = Prime64_4;
        acc[5] = Prime32_2;
        acc[6] = Prime64_5;
        acc[7] = Prime32_1;
    }

    private static void Accumulate(ulong[] acc, ReadOnlySpan<byte> source, int secretBase, int secretOffset, int stripesToProcess)
    {
        int offset = 0;
        for (int i = 0; i < stripesToProcess; i++)
        {
            Accumulate512(acc, source[offset..], secretBase + secretOffset + i * SecretConsumeRateBytes);
            offset += StripeLengthBytes;
        }
    }

    private static void ConsumeStripes(
        ulong[] acc, ref ulong stripesSoFar, ulong stripesPerBlock,
        ReadOnlySpan<byte> source, ulong stripes, int secretBase)
    {
        ulong stripesToEndOfBlock = stripesPerBlock - stripesSoFar;
        if (stripesToEndOfBlock <= stripes)
        {
            Accumulate(acc, source, secretBase, (int)stripesSoFar * SecretConsumeRateBytes, (int)stripesToEndOfBlock);
            ScrambleAccumulators(acc);
            int consumed = (int)stripesToEndOfBlock * StripeLengthBytes;
            Accumulate(acc, source[consumed..], secretBase, 0, (int)(stripes - stripesToEndOfBlock));
            stripesSoFar = stripes - stripesToEndOfBlock;
        }
        else
        {
            Accumulate(acc, source, secretBase, (int)stripesSoFar * SecretConsumeRateBytes, (int)stripes);
            stripesSoFar += stripes;
        }
    }

    private static void Accumulate512(ulong[] acc, ReadOnlySpan<byte> source, int secretOffset)
    {
        for (int i = 0; i < AccumulatorCount; i++)
        {
            ulong sourceVal = BinaryPrimitives.ReadUInt64LittleEndian(source[(8 * i)..]);
            ulong sourceKey = sourceVal ^ ReadSecret(secretOffset + i * 8);

            acc[i ^ 1] += sourceVal; // 交换相邻 lane
            acc[i] += Multiply32To64((uint)sourceKey, (uint)(sourceKey >> 32));
        }
    }

    private static void ScrambleAccumulators(ulong[] acc)
    {
        for (int i = 0; i < AccumulatorCount; i++)
        {
            ulong xorShift = XorShift(acc[i], 47);
            ulong xorWithKey = xorShift ^ ReadSecret(SecretLengthBytes - StripeLengthBytes + i * 8);
            acc[i] = xorWithKey * Prime32_1;
        }
    }

    private static ulong MergeAccumulators(ulong[] acc, int secretOffset, ulong start)
    {
        ulong result = start;

        result += Multiply64To128ThenFold(acc[0] ^ ReadSecret(secretOffset), acc[1] ^ ReadSecret(secretOffset + 8));
        result += Multiply64To128ThenFold(acc[2] ^ ReadSecret(secretOffset + 16), acc[3] ^ ReadSecret(secretOffset + 24));
        result += Multiply64To128ThenFold(acc[4] ^ ReadSecret(secretOffset + 32), acc[5] ^ ReadSecret(secretOffset + 40));
        result += Multiply64To128ThenFold(acc[6] ^ ReadSecret(secretOffset + 48), acc[7] ^ ReadSecret(secretOffset + 56));

        return Avalanche(result);
    }

    private static ulong ReadSecret(int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(DefaultSecret.AsSpan(offset));

    // ===== 基础运算 =====

    private static ulong Avalanche(ulong hash)
    {
        hash = XorShift(hash, 37);
        hash *= 0x165667919E3779F9UL;
        hash = XorShift(hash, 32);
        return hash;
    }

    private static ulong Avalanche64(ulong hash)
    {
        hash = XorShift(hash, 33);
        hash *= Prime64_2;
        hash = XorShift(hash, 29);
        hash *= Prime64_3;
        return XorShift(hash, 32);
    }

    private static ulong XorShift(ulong value, int shift) => value ^ (value >> shift);

    private static ulong Multiply32To64(uint v1, uint v2) => (ulong)v1 * v2;

    private static ulong Multiply64To128(ulong left, ulong right, out ulong lower)
    {
        ulong upper = Math.BigMul(left, right, out lower);
        return upper;
    }

    private static ulong Multiply64To128ThenFold(ulong left, ulong right)
    {
        ulong upper = Multiply64To128(left, right, out ulong lower);
        return lower ^ upper;
    }
}
