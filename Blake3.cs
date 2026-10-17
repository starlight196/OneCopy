// OneCopy — 重复文件清理工具
// Copyright (C) 2026 starlight196
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Numerics;

namespace OneCopy;

/// <summary>
/// BLAKE3（默认 32 字节输出模式）的托管实现，用于重复文件查找的加密级快速哈希。
/// 按 BLAKE3 规范实现分块压缩与 Merkle 树；已用官方测试向量验证。
/// 参考：https://github.com/BLAKE3-team/BLAKE3 （CC0 / Apache-2.0）
/// </summary>
internal sealed class Blake3Hasher
{
    public const int HashSizeInBytes = 32;

    private const int BlockLength = 64;
    private const int ChunkLength = 1024;
    private const uint FlagChunkStart = 1;
    private const uint FlagChunkEnd = 2;
    private const uint FlagParent = 4;
    private const uint FlagRoot = 8;

    private static readonly uint[] IV =
    {
        0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
        0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19,
    };

    private static readonly byte[] MsgPermutation = { 2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8 };

    private readonly byte[] _chunk = new byte[ChunkLength];
    private readonly List<byte[]> _cvStack = new(32);
    private int _chunkLength;
    private ulong _chunkIndex;

    public void Reset()
    {
        _chunkLength = 0;
        _chunkIndex = 0;
        _cvStack.Clear();
    }

    public void Update(ReadOnlySpan<byte> source)
    {
        while (source.Length > 0)
        {
            if (_chunkLength == ChunkLength)
            {
                AddChunkChainingValue(
                    ChunkOutput(_chunk, ChunkLength, _chunkIndex).ChainingValue(),
                    _chunkIndex + 1);
                _chunkIndex++;
                _chunkLength = 0;
            }

            int take = Math.Min(ChunkLength - _chunkLength, source.Length);
            source[..take].CopyTo(_chunk.AsSpan(_chunkLength));
            _chunkLength += take;
            source = source[take..];
        }
    }

    /// <summary>完成计算并返回 32 字节哈希。</summary>
    public byte[] GetHash()
    {
        Output output = ChunkOutput(_chunk, _chunkLength, _chunkIndex);
        for (int i = _cvStack.Count - 1; i >= 0; i--)
        {
            output = ParentOutput(_cvStack[i], output.ChainingValue());
        }

        byte[] hash = new byte[HashSizeInBytes];
        output.RootOutputBytes(hash);
        return hash;
    }

    /// <summary>一次性计算 BLAKE3。</summary>
    public static byte[] Hash(ReadOnlySpan<byte> source)
    {
        var hasher = new Blake3Hasher();
        hasher.Update(source);
        return hasher.GetHash();
    }

    // ===== Merkle 树 =====

    private void AddChunkChainingValue(byte[] cv, ulong totalChunks)
    {
        while ((totalChunks & 1) == 0)
        {
            byte[] left = _cvStack[^1];
            _cvStack.RemoveAt(_cvStack.Count - 1);
            cv = ParentOutput(left, cv).ChainingValue();
            totalChunks >>= 1;
        }

        _cvStack.Add(cv);
    }

    private static Output ParentOutput(byte[] left, byte[] right)
    {
        uint[] block = new uint[16];
        for (int i = 0; i < 8; i++)
        {
            block[i] = BinaryPrimitives.ReadUInt32LittleEndian(left.AsSpan(i * 4));
            block[8 + i] = BinaryPrimitives.ReadUInt32LittleEndian(right.AsSpan(i * 4));
        }

        return new Output(IV, block, BlockLength, counter: 0, FlagParent);
    }

    private static Output ChunkOutput(byte[] chunk, int length, ulong chunkIndex)
    {
        uint[] cv = (uint[])IV.Clone();

        int fullBlocks = length == 0 ? 0 : (length - 1) / BlockLength;
        Span<uint> compressed = stackalloc uint[16];
        for (int i = 0; i < fullBlocks; i++)
        {
            uint[] block = LoadBlock(chunk, i * BlockLength, BlockLength);
            uint flags = i == 0 ? FlagChunkStart : 0;
            Compress(cv, block, BlockLength, chunkIndex, flags, compressed);
            for (int j = 0; j < 8; j++) cv[j] = compressed[j];
        }

        int finalLength = length - fullBlocks * BlockLength;
        uint[] finalBlock = LoadBlock(chunk, fullBlocks * BlockLength, finalLength);
        uint finalFlags = FlagChunkEnd | (fullBlocks == 0 ? FlagChunkStart : 0);
        return new Output(cv, finalBlock, (uint)finalLength, chunkIndex, finalFlags);
    }

    private static uint[] LoadBlock(byte[] data, int offset, int length)
    {
        var words = new uint[16];
        Span<byte> bytes = stackalloc byte[BlockLength];
        bytes.Clear();
        if (length > 0) data.AsSpan(offset, length).CopyTo(bytes);
        for (int i = 0; i < 16; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i * 4)..]);
        }
        return words;
    }

    // ===== 压缩函数 =====

    private static void Compress(
        uint[] cv,
        uint[] block,
        uint blockLength,
        ulong counter,
        uint flags,
        Span<uint> output)
    {
        Span<uint> v = stackalloc uint[16];
        Span<uint> m = stackalloc uint[16];
        Span<uint> permuted = stackalloc uint[16];

        for (int i = 0; i < 8; i++) v[i] = cv[i];
        v[8] = IV[0];
        v[9] = IV[1];
        v[10] = IV[2];
        v[11] = IV[3];
        v[12] = (uint)counter;
        v[13] = (uint)(counter >> 32);
        v[14] = blockLength;
        v[15] = flags;

        for (int i = 0; i < 16; i++) m[i] = block[i];

        for (int round = 0; round < 7; round++)
        {
            G(ref v[0], ref v[4], ref v[8], ref v[12], m[0], m[1]);
            G(ref v[1], ref v[5], ref v[9], ref v[13], m[2], m[3]);
            G(ref v[2], ref v[6], ref v[10], ref v[14], m[4], m[5]);
            G(ref v[3], ref v[7], ref v[11], ref v[15], m[6], m[7]);
            G(ref v[0], ref v[5], ref v[10], ref v[15], m[8], m[9]);
            G(ref v[1], ref v[6], ref v[11], ref v[12], m[10], m[11]);
            G(ref v[2], ref v[7], ref v[8], ref v[13], m[12], m[13]);
            G(ref v[3], ref v[4], ref v[9], ref v[14], m[14], m[15]);

            if (round < 6)
            {
                for (int i = 0; i < 16; i++) permuted[i] = m[MsgPermutation[i]];
                permuted.CopyTo(m);
            }
        }

        for (int i = 0; i < 8; i++)
        {
            v[i] ^= v[i + 8];
            v[i + 8] ^= cv[i];
        }

        v.CopyTo(output);
    }

    private static void G(ref uint a, ref uint b, ref uint c, ref uint d, uint mx, uint my)
    {
        a = a + b + mx;
        d = BitOperations.RotateRight(d ^ a, 16);
        c = c + d;
        b = BitOperations.RotateRight(b ^ c, 12);
        a = a + b + my;
        d = BitOperations.RotateRight(d ^ a, 8);
        c = c + d;
        b = BitOperations.RotateRight(b ^ c, 7);
    }

    // ===== 输出 =====

    private readonly struct Output
    {
        private readonly uint[] _inputCv;
        private readonly uint[] _block;
        private readonly uint _blockLength;
        private readonly ulong _counter;
        private readonly uint _flags;

        public Output(uint[] inputCv, uint[] block, uint blockLength, ulong counter, uint flags)
        {
            _inputCv = inputCv;
            _block = block;
            _blockLength = blockLength;
            _counter = counter;
            _flags = flags;
        }

        /// <summary>该输出对应的链值（用于组合父节点）。</summary>
        public byte[] ChainingValue()
        {
            Span<uint> words = stackalloc uint[16];
            Compress(_inputCv, _block, _blockLength, _counter, _flags, words);

            byte[] cv = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(cv.AsSpan(i * 4), words[i]);
            }
            return cv;
        }

        /// <summary>根输出：32 字节哈希。</summary>
        public void RootOutputBytes(Span<byte> destination)
        {
            Span<uint> words = stackalloc uint[16];
            Compress(_inputCv, _block, _blockLength, _counter, _flags | FlagRoot, words);
            for (int i = 0; i < 8; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(destination[(i * 4)..], words[i]);
            }
        }
    }
}
