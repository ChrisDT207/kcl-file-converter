using System;
using System.Text.RegularExpressions;

namespace ZooStepConverter.BusinessLogic.Common
{
    /// <summary>
    /// Business Logic Layer: Common parsing, string formatting, and error extraction utilities.
    /// </summary>
    public static class TextHelpers
    {
        public static string JsonStr(string json, string key)
        {
            if (json == null) return null;
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(no output)";
            int i = s.IndexOf('\n');
            return (i < 0 ? s : s.Substring(0, i)).Trim();
        }

        public static string CleanErrorSummary(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(no output)";
            // Extract semantic message if present
            var m = Regex.Match(s, @"message:\s*""([^""]+)""");
            if (m.Success) return m.Groups[1].Value;

            return FirstLine(s);
        }

        public static string Truncate(string s, int n)
        {
            if (s == null) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "...";
        }
    }
}

