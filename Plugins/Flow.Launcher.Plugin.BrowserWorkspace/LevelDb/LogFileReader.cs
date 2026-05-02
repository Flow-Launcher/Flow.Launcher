using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserWorkspace.LevelDb;

/// <summary>
/// Reads key-value entries from a LevelDB Write-Ahead Log (.log) file.
/// Format: https://github.com/google/leveldb/blob/master/doc/log_format.md
/// </summary>
internal static class LogFileReader
{
    private const int LogBlockSize = 32768;
    private const int RecordHeaderSize = 7; // 4-byte CRC + 2-byte length + 1-byte type

    private enum RecordType : byte
    {
        Zero = 0,
        Full = 1,
        First = 2,
        Middle = 3,
        Last = 4
    }

    /// <summary>
    /// Reads all put (non-delete) entries from the log file.
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
            foreach (var batch in ReadBatches(stream))
            {
                foreach (var entry in ParseBatch(batch))
                    yield return entry;
            }
        }
    }

    // -------------------------------------------------------------------
    // Batch assembly from log records

    private static IEnumerable<byte[]> ReadBatches(Stream stream)
    {
        byte[] headerBuf = new byte[RecordHeaderSize];
        List<byte[]> fragments = new();

        while (true)
        {
            long blockStart = (stream.Position / LogBlockSize) * LogBlockSize;
            long offsetInBlock = stream.Position - blockStart;

            // Skip trailer padding at the end of a block (< 7 bytes remaining)
            if (LogBlockSize - offsetInBlock < RecordHeaderSize)
            {
                long nextBlock = blockStart + LogBlockSize;
                stream.Seek(nextBlock, SeekOrigin.Begin);
                if (stream.Position >= stream.Length) yield break;
                continue;
            }

            if (!ReadFully(stream, headerBuf)) yield break;

            // CRC (4 bytes) – we skip verification
            ushort length = BitConverter.ToUInt16(headerBuf, 4);
            var type = (RecordType)headerBuf[6];

            if (type == RecordType.Zero)
            {
                // Padding – skip to end of block
                long nextBlock = ((stream.Position - 1) / LogBlockSize + 1) * LogBlockSize;
                stream.Seek(nextBlock, SeekOrigin.Begin);
                if (stream.Position >= stream.Length) yield break;
                continue;
            }

            byte[] payload = new byte[length];
            if (!ReadFully(stream, payload)) yield break;

            switch (type)
            {
                case RecordType.Full:
                    fragments.Clear();
                    yield return payload;
                    break;
                case RecordType.First:
                    fragments.Clear();
                    fragments.Add(payload);
                    break;
                case RecordType.Middle:
                    fragments.Add(payload);
                    break;
                case RecordType.Last:
                    fragments.Add(payload);
                    yield return AssembleFragments(fragments);
                    fragments.Clear();
                    break;
            }
        }
    }

    private static byte[] AssembleFragments(List<byte[]> fragments)
    {
        int total = 0;
        foreach (var f in fragments) total += f.Length;
        byte[] result = new byte[total];
        int pos = 0;
        foreach (var f in fragments)
        {
            Buffer.BlockCopy(f, 0, result, pos, f.Length);
            pos += f.Length;
        }
        return result;
    }

    // -------------------------------------------------------------------
    // WriteBatch decoding
    // Format: SequenceNumber(8) + Count(4) + entries...
    // Each entry: type(1) + key(varint-len + bytes) [+ value(varint-len + bytes) if type==1]

    private static IEnumerable<(byte[] UserKey, byte[] Value, ulong SeqNum)> ParseBatch(byte[] batch)
    {
        if (batch.Length < 12) yield break;

        ulong seqNum = BitConverter.ToUInt64(batch, 0);
        // uint count = BitConverter.ToUInt32(batch, 8); // not strictly needed

        int offset = 12;
        while (offset < batch.Length)
        {
            byte entryType = batch[offset++];
            if (!TryReadLengthPrefixed(batch, ref offset, out byte[] key)) yield break;

            if (entryType == 1) // kTypeValue
            {
                if (!TryReadLengthPrefixed(batch, ref offset, out byte[] value)) yield break;
                yield return (key, value, seqNum);
            }
            // entryType == 0 is kTypeDeletion – skip (no value bytes follow)
        }
    }

    // -------------------------------------------------------------------
    // Helpers

    private static bool TryReadLengthPrefixed(byte[] data, ref int offset, out byte[] result)
    {
        result = null;
        if (!TryReadVarint32(data, ref offset, out uint len)) return false;
        if (offset + (int)len > data.Length) return false;
        result = new byte[len];
        Buffer.BlockCopy(data, offset, result, 0, (int)len);
        offset += (int)len;
        return true;
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
}
