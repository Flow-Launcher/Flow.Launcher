using System;

namespace Flow.Launcher.Plugin.BrowserWorkspace.LevelDb;

/// <summary>
/// Minimal pure-managed Snappy decompressor for LevelDB block data.
/// Implements the raw Snappy format (no framing headers).
/// </summary>
internal static class SnappyDecoder
{
    /// <summary>Decompress a raw Snappy-compressed buffer.</summary>
    public static byte[] Decompress(byte[] input)
    {
        int offset = 0;
        int outputLen = ReadVarint32(input, ref offset);

        byte[] output = new byte[outputLen];
        int outPos = 0;

        while (offset < input.Length && outPos < outputLen)
        {
            byte tag = input[offset++];
            int elementType = tag & 0x03;

            if (elementType == 0) // Literal
            {
                int lenPart = (tag >> 2) & 0x3F;
                int literalLen;
                if (lenPart < 60)
                {
                    literalLen = lenPart + 1;
                }
                else if (lenPart == 60)
                {
                    literalLen = input[offset++] + 1;
                }
                else if (lenPart == 61)
                {
                    literalLen = (input[offset] | (input[offset + 1] << 8)) + 1;
                    offset += 2;
                }
                else if (lenPart == 62)
                {
                    literalLen = (input[offset] | (input[offset + 1] << 8) | (input[offset + 2] << 16)) + 1;
                    offset += 3;
                }
                else // lenPart == 63
                {
                    literalLen = (int)((uint)(input[offset] | (input[offset + 1] << 8) |
                                              (input[offset + 2] << 16) | (input[offset + 3] << 24))) + 1;
                    offset += 4;
                }

                Buffer.BlockCopy(input, offset, output, outPos, literalLen);
                offset += literalLen;
                outPos += literalLen;
            }
            else if (elementType == 1) // Copy with 1-byte offset
            {
                int copyLen = 4 + ((tag >> 2) & 0x07);
                int copyOffset = ((tag >> 5) & 0x07) << 8 | input[offset++];
                OverlapCopy(output, outPos, outPos - copyOffset, copyLen);
                outPos += copyLen;
            }
            else if (elementType == 2) // Copy with 2-byte offset
            {
                int copyLen = 1 + ((tag >> 2) & 0x0F);
                int copyOffset = input[offset] | (input[offset + 1] << 8);
                offset += 2;
                OverlapCopy(output, outPos, outPos - copyOffset, copyLen);
                outPos += copyLen;
            }
            else // elementType == 3, Copy with 4-byte offset
            {
                int copyLen = 1 + ((tag >> 2) & 0x0F);
                int copyOffset = (int)((uint)(input[offset] | (input[offset + 1] << 8) |
                                              (input[offset + 2] << 16) | (input[offset + 3] << 24)));
                offset += 4;
                OverlapCopy(output, outPos, outPos - copyOffset, copyLen);
                outPos += copyLen;
            }
        }

        return output;
    }

    // Byte-by-byte copy to handle overlapping ranges (run-length encoding).
    private static void OverlapCopy(byte[] buf, int dst, int src, int len)
    {
        for (int i = 0; i < len; i++)
            buf[dst + i] = buf[src + i];
    }

    private static int ReadVarint32(byte[] data, ref int offset)
    {
        int result = 0;
        int shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift >= 35) throw new InvalidOperationException("Varint too long");
        }
        return result;
    }
}
