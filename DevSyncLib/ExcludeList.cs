﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevSyncLib
{
    public class ExcludeList
    {
        private Regex _regex;
        private readonly List<string> _masks = new List<string>();

        protected string MaskToRegex(string mask)
        {
            var re = Regex.Escape(FsEntry.NormalizePath(mask))
                // *
                .Replace("\\*", "[^/]*")
                // ?
                .Replace("\\?", "[^/]");

            // slash in the beginning -> begin of string
            if (re.StartsWith("/"))
            {
                re = '^' + re.Substring(1);
            }
            else
            {
                // begin of string or slash before mask
                re = "(^|/)" + re;
            }

            // end of string or slash after mask
            re += "($|/)";
            return re;
        }

        public List<string> GetList()
        {
            return _masks;
        }

        public bool SetList(List<string> list)
        {
            _masks.Clear();
            var re = list.Count > 0 ? list.Select(MaskToRegex).Aggregate((current, next) => current + "|" + next) : null;
            try
            {
                _regex = string.IsNullOrEmpty(re) ? null : new Regex(re, RegexOptions.Compiled);
                _masks.AddRange(list);
                return true;
            }
            catch (Exception)
            {
                Console.Error.WriteLine($"Invalid exclude list: {re}");
                _regex = null;
                return false;
            }
        }

        public bool IsExcluded(string path)
        {
            return !string.IsNullOrEmpty(path) && _regex != null && _regex.IsMatch(path);
        }
    }
}
