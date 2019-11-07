using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DevSyncLib
{
    public class FileMaskList
    {
        private Regex _regex;
        private readonly List<string> _masks = new List<string>();

        protected StringBuilder MaskToRegex(string mask)
        {
            var re = new StringBuilder(Regex.Escape(FsEntry.NormalizePath(mask)))
                // *
                .Replace("\\*", "[^/]*")
                // ?
                .Replace("\\?", "[^/]");

            // slash in the beginning -> begin of string
            re.Insert(0, re[0] == '/' ? '^' : '/');

            // slash after mask
            if (re[re.Length - 1] != '/')
            {
                re.Append('/');
            }

            return re;
        }

        public List<string> GetList()
        {
            return _masks;
        }

        public bool SetList(List<string> list)
        {
            _masks.Clear();
            var combinedRegex = new StringBuilder();
            foreach (var mask in list)
            {
                var trimmedMask = mask.Trim();
                if (!string.IsNullOrEmpty(trimmedMask))
                {
                    var re = MaskToRegex(trimmedMask);
                    if (combinedRegex.Length > 0)
                    {
                        combinedRegex.Append('|');
                    }

                    combinedRegex.Append(re);
                }
            }

            try
            {
                _regex = combinedRegex.Length == 0 ? null : new Regex(combinedRegex.ToString(), RegexOptions.Compiled);
                _masks.AddRange(list);
                return true;
            }
            catch (Exception)
            {
                _regex = null;
                return false;
            }
        }

        public bool IsMatch(string path)
        {
            if (string.IsNullOrEmpty(path) || _regex == null)
            {
                return false;
            }
            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }
            if (!path.EndsWith('/'))
            {
                path += "/";
            }
            return _regex.IsMatch(path);
        }
    }
}
