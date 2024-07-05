using System;
using System.Collections.Generic;

namespace DevSyncLib;

public class FileMaskList
{
    private readonly List<Mask> _masks = [];
    private readonly List<Mask> _negativeMasks = [];

    private class Mask
    {
        public readonly bool HasSlash;
        public readonly string Value;
        public Mask(string value)
        {
            HasSlash = value.Contains('/');

            // remove first /
            if (value.StartsWith('/'))
            {
                value = value[1..];
            }

            Value = value;
        }
    }

    public void Clear()
    {
        _masks.Clear();
        _negativeMasks.Clear();
    }

    public bool SetList(List<string> list)
    {
        Clear();
        foreach (var item in list)
        {
            var maskValue = item.TrimStart();
            if (!string.IsNullOrWhiteSpace(maskValue) && !maskValue.StartsWith('#'))
            {
                bool negative;
                if (maskValue.StartsWith('!'))
                {
                    negative = true;
                    maskValue = maskValue[1..];
                }
                else
                {
                    negative = false;
                }

                var mask = new Mask(maskValue);
                if (negative)
                {
                    _negativeMasks.Add(mask);
                }
                else
                {
                    _masks.Add(mask);
                }
            }
        }

        return true;
    }

    private static unsafe bool MatchMask(char* textStart, char* textEnd, Mask mask)
    {
        if (mask.HasSlash)
        {
            return MatchTextMask(textStart, textEnd, mask);
        }

        // match every basename in text
        var start = textStart;
        for (var p = textStart; p < textEnd; p++)
        {
            if (*p == '/' || *p == '\\')
            {
                if (start < p && MatchTextMask(start, p, mask))
                {
                    return true;
                }

                start = p + 1;
            }
        }

        return start < textEnd && MatchTextMask(start, textEnd, mask);
    }

    /*
     * https://www.codeproject.com/Articles/5163931/Fast-String-Matching-with-Wildcards-Globs-and-Giti
     *
     * Modified
     */
    private static unsafe bool MatchTextMask(char* textStart, char* textEnd, Mask mask)
    {
        char* text1Backup = null;
        char* glob1Backup = null;

        char* text2Backup = null;
        char* glob2Backup = null;

        fixed (char* globStart = mask.Value)
        {
            var globPointer = globStart;
            var globEnd = globPointer + mask.Value.Length;
            var textPointer = textStart;

            while (textPointer < textEnd)
            {
                if (globPointer < globEnd)
                {
                    switch (*globPointer)
                    {
                        case '*':
                            if (++globPointer < globEnd && *globPointer == '*')
                            {
                                // trailing ** match everything after /
                                if (++globPointer >= globEnd)
                                {
                                    return true;
                                }

                                // ** followed by a / match zero or more directories
                                if (*globPointer != '/' && *globPointer != '\\')
                                {
                                    return false;
                                }

                                // new **-loop, discard *-loop
                                text1Backup = null;
                                glob1Backup = null;
                                text2Backup = textPointer;
                                glob2Backup = ++globPointer;
                                continue;
                            }

                            // trailing * matches everything except /
                            text1Backup = textPointer;
                            glob1Backup = globPointer;
                            continue;
                        case '?':
                            // match any character except /
                            if (*textPointer == '/' || *textPointer == '\\')
                            {
                                break;
                            }

                            textPointer++;
                            globPointer++;
                            continue;
                        default:
                            if (*globPointer == '\\')
                            {
                                // literal match \-escaped character
                                if (globPointer + 1 < globEnd)
                                {
                                    globPointer++;
                                }
                            }

                            // match the current non-NUL character
                            if (*globPointer != *textPointer && (*globPointer != '/' && *globPointer != '\\' || *textPointer != '/' && *textPointer != '\\'))
                                break;

                            textPointer++;
                            globPointer++;
                            continue;
                    }
                }

                if (glob1Backup != null && *text1Backup != '/' && *text1Backup != '\\')
                {
                    // *-loop: backtrack to the last * but do not jump over /
                    textPointer = ++text1Backup;
                    globPointer = glob1Backup;
                    continue;
                }

                if (glob2Backup != null)
                {
                    // **-loop: backtrack to the last **
                    textPointer = ++text2Backup;
                    globPointer = glob2Backup;
                    continue;
                }

                // partial path match
                if (textPointer < textEnd && (*textPointer == '/' || *textPointer == '\\'))
                {
                    break;
                }

                return false;
            }

            // ignore trailing stars
            while (globPointer < globEnd && *globPointer == '*')
            {
                globPointer++;
            }

            // at end of text means success if nothing else is left to match
            return globPointer >= globEnd;
        }
    }

    private static unsafe bool MatchTextMasks(char* textStart, char* textEnd, IEnumerable<Mask> masks)
    {
        foreach (var mask in masks)
        {
            if (MatchMask(textStart, textEnd, mask))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsMatch(string? path)
    {
        return path != null && IsMatch(path.AsSpan());
        }

    public bool IsMatch(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty)
        {
            return false;
        }

        unsafe
        {
            fixed (char* pathStart = path)
            {
                var pathEnd = pathStart + path.Length;
                return !MatchTextMasks(pathStart, pathEnd, _negativeMasks) &&
                       MatchTextMasks(pathStart, pathEnd, _masks);
            }
        }
    }
}