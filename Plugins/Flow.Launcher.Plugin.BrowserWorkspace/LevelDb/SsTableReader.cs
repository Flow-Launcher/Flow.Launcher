using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserWorkspace.LevelDb;

/// <summary>
/// Reads key-value entries from a LevelDB SSTable (.ldb) file.
/// Format: https://github.com/google/leveldb/blob/master/doc/table_format.md
/// </summary>
internal static class SsTableReader
{
    private const ulong MagicNumber = 0xdb4775248b80fb57UL;
    private const int FooterSize = 48;
    private const int BlockTrailerSize = 5; // 1-byte compression type + 4-byte CRC

    /// <summary>
    /// Reads all live (non-deleted) entries from the SSTable file.
    /// Each entry is (userKey, value, sequenceNumber).
    /// </summary>
    public static IEnumerable<(byte[] UserKey, byte[] Value, ulong SeqNum)> ReadEntries(string path)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException)
        {
            yield break;
        }

        using (stream)
        {
            long fileSize = stream.Length;
            if (fileSize < FooterSize) yield break;

            // Read footer (last 48 bytes)
            stream.Seek(-FooterSize, SeekOrigin.End);
            byte[] footer = new byte[FooterSize];
            if (!ReadFully(stream, footer)) yield break;

            // Verify magic number at footer[40..48]
            ulong magic = BitConverter.ToUInt64(footer, 40);
            if (magic != MagicNumber) yield break;

            // Parse metaindex handle (not used) and index handle from footer
            int footerPos = 0;
            SkipBlockHandle(footer, ref footerPos); // metaindex – skip
            ReadBlockHandle(footer, ref footerPos, out long indexOffset, out long indexSize);

            // Read and parse the index block
            byte[] indexData = ReadBlock(stream, indexOffset, indexSize);
            if (indexData == null) yield break;

            // Each index entry value is a BlockHandle pointing to a data block
            foreach (var (_, handleBytes) in IterateBlockEntries(indexData))
            {
                int hp = 0;
                ReadBlockHandle(handleBytes, ref hp, out long dataOffset, out long dataSize);

                byte[] dataBlock = ReadBlock(stream, dataOffset, dataSize);
                if (dataBlock == null) continue;

                foreach (var (internalKey, value) in IterateBlockEntries(dataBlock))
                {
                    // Internal key format: user_key + 8-byte (seq<<8 | type)
                    if (internalKey.Length < 8) continue;

                    ulong seqAndType = BitConverter.ToUInt64(internalKey, internalKey.Length - 8);
                    byte valueType = (byte)(seqAndType & 0xFF);
                    ulong seqNum = seqAndType >> 8;

                    if (valueType == 0) continue; // Deletion marker – skip

                    byte[] userKey = new byte[internalKey.Length - 8];
                    Buffer.BlockCopy(internalKey, 0, userKey, 0, userKey.Length);

                    yield return (userKey, value, seqNum);
                }
            }
        }
    }

    // -------------------------------------------------------------------
    // Block reading

    private static byte[] ReadBlock(FileStream stream, long offset, long size)
    {
        try
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] compressed = new byte[size];
            if (!ReadFully(stream, compressed)) return null;

            byte[] trailer = new byte[BlockTrailerSize];
            if (!ReadFully(stream, trailer)) return null;

            byte compressionType = trailer[0];
            return compressionType switch
            {
                0 => compressed, // No compression
                1 => SnappyDecoder.Decompress(compressed), // Snappy
                _ => null // Unknown compression
            };
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------
    // Block entry iteration (prefix-compressed key-value pairs)

    private static IEnumerable<(byte[] Key, byte[] Value)> IterateBlockEntries(byte[] block)
    {
        if (block.Length < 4) yield break;

        // Last 4 bytes of block data = number of restart points
        uint numRestarts = BitConverter.ToUInt32(block, block.Length - 4);
        int restartArrayOffset = block.Length - (int)((numRestarts + 1) * 4);
        if (restartArrayOffset < 0 || restartArrayOffset > block.Length) yield break;

        int offset = 0;
        byte[] currentKey = Array.Empty<byte>();

        while (offset < restartArrayOffset)
        {
            if (!TryReadVarint32(block, ref offset, out uint shared)) break;
            if (!TryReadVarint32(block, ref offset, out uint nonShared)) break;
            if (!TryReadVarint32(block, ref offset, out uint valueLen)) break;

            int needed = (int)nonShared + (int)valueLen;
            if (offset + needed > restartArrayOffset) break;

            byte[] key = new byte[(int)shared + (int)nonShared];
            if (shared > 0 && shared <= currentKey.Length)
                Buffer.BlockCopy(currentKey, 0, key, 0, (int)shared);
            Buffer.BlockCopy(block, offset, key, (int)shared, (int)nonShared);
            offset += (int)nonShared;

            byte[] value = new byte[(int)valueLen];
            Buffer.BlockCopy(block, offset, value, 0, (int)valueLen);
            offset += (int)valueLen;

            currentKey = key;
            yield return (key, value);
        }
    }

    // -------------------------------------------------------------------
    // Helpers

    private static bool ReadFully(Stream stream, byte[] buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = stream.Read(buffer, read, buffer.Length - read);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    private static void ReadBlockHandle(byte[] data, ref int offset, out long blockOffset, out long blockSize)
    {
        blockOffset = (long)ReadVarint64(data, ref offset);
        blockSize = (long)ReadVarint64(data, ref offset);
    }

    private static void SkipBlockHandle(byte[] data, ref int offset)
    {
        ReadVarint64(data, ref offset);
        ReadVarint64(data, ref offset);
    }

    private static bool TryReadVarint32(byte[] data, ref int offset, out uint value)
    {
        value = 0;
        int shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
            if (shift >= 35) return false;
        }
        return false;
    }

    private static ulong ReadVarint64(byte[] data, ref int offset)
    {
        ulong result = 0;
        int shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        return result;
    }
}
