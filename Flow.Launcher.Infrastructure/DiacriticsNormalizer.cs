using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Infrastructure;

public static class DiacriticsNormalizer
{
    private static readonly Dictionary<char, char> AccentMap = new()
    {
        ['á'] = 'a',
        ['à'] = 'a',
        ['ã'] = 'a',
        ['â'] = 'a',
        ['ä'] = 'a',
        ['å'] = 'a',
        ['ā'] = 'a',
        ['ă'] = 'a',
        ['ą'] = 'a',
        ['é'] = 'e',
        ['è'] = 'e',
        ['ê'] = 'e',
        ['ë'] = 'e',
        ['ē'] = 'e',
        ['ĕ'] = 'e',
        ['ė'] = 'e',
        ['ę'] = 'e',
        ['ě'] = 'e',
        ['í'] = 'i',
        ['ì'] = 'i',
        ['î'] = 'i',
        ['ï'] = 'i',
        ['ī'] = 'i',
        ['ĭ'] = 'i',
        ['į'] = 'i',
        ['ı'] = 'i',
        ['ó'] = 'o',
        ['ò'] = 'o',
        ['õ'] = 'o',
        ['ô'] = 'o',
        ['ö'] = 'o',
        ['ø'] = 'o',
        ['ō'] = 'o',
        ['ŏ'] = 'o',
        ['ő'] = 'o',
        ['ú'] = 'u',
        ['ù'] = 'u',
        ['û'] = 'u',
        ['ü'] = 'u',
        ['ū'] = 'u',
        ['ŭ'] = 'u',
        ['ů'] = 'u',
        ['ű'] = 'u',
        ['ų'] = 'u',
        ['ç'] = 'c',
        ['ć'] = 'c',
        ['ĉ'] = 'c',
        ['ċ'] = 'c',
        ['č'] = 'c',
        ['ñ'] = 'n',
        ['ń'] = 'n',
        ['ņ'] = 'n',
        ['ň'] = 'n',
        ['ŋ'] = 'n',
        ['ý'] = 'y',
        ['ÿ'] = 'y',
        ['ŷ'] = 'y',
        ['ś'] = 's',
        ['ŝ'] = 's',
        ['ş'] = 's',
        ['š'] = 's',
        ['ß'] = 's',
        ['ź'] = 'z',
        ['ż'] = 'z',
        ['ž'] = 'z',
        ['ł'] = 'l',
        ['ď'] = 'd',
        ['đ'] = 'd',
        ['ĝ'] = 'g',
        ['ğ'] = 'g',
        ['ġ'] = 'g',
        ['ģ'] = 'g',
        ['ĥ'] = 'h',
        ['ħ'] = 'h',
        ['ĵ'] = 'j',
        ['ķ'] = 'k',
        ['ŕ'] = 'r',
        ['ř'] = 'r',
        ['ţ'] = 't',
        ['ť'] = 't',
        ['ŧ'] = 't',
        ['æ'] = 'a',
        ['œ'] = 'o'
    };

    private const char AccentRangeStart = '\u00DF';
    private const char AccentRangeEnd = '\u017E';
    private static readonly char[] AccentLookup = BuildAccentLookup();

    private static char[] BuildAccentLookup()
    {
        var lookup = new char[AccentRangeEnd - AccentRangeStart + 1];
        foreach (var (key, value) in AccentMap)
            lookup[key - AccentRangeStart] = value;
        return lookup;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char NormalizeChar(char c)
    {
        c = char.ToLowerInvariant(c);
        if (c >= AccentRangeStart && c <= AccentRangeEnd)
        {
            var mapped = AccentLookup[c - AccentRangeStart];
            if (mapped != 0) return mapped;
        }

        return c;
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        int firstChange = -1;
        for (int i = 0; i < value.Length; i++)
        {
            if (NormalizeChar(value[i]) != value[i])
            {
                firstChange = i;
                break;
            }
        }

        if (firstChange < 0) return value;

        char[] arrayFromPool = null;
        Span<char> buffer = value.Length <= 512
            ? stackalloc char[value.Length]
            : (arrayFromPool = ArrayPool<char>.Shared.Rent(value.Length));
        try
        {
            value.AsSpan(0, firstChange).CopyTo(buffer);
            for (int i = firstChange; i < value.Length; i++)
                buffer[i] = NormalizeChar(value[i]);

            return new string(buffer.Slice(0, value.Length));
        }
        finally
        {
            if (arrayFromPool != null)
                ArrayPool<char>.Shared.Return(arrayFromPool);
        }
    }
}
