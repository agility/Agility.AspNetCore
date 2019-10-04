using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Agility.Web.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string str, int length, string appendStr, bool stripHtml, bool stopAtWordBreak)
        {
            string result = stripHtml ? str.StripHtml() : str;

            if (result.Length <= length) return result;

            result = result.Substring(0, length);
            result = stopAtWordBreak ? StopAtWordBreak(result) : result;
            return AppendString(result, appendStr);
        }

        public static string StripHtml(this string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            string s = Regex.Replace(str, "<[^>]*>", "").StripLineBreaks();
			return HttpUtility.HtmlDecode(s);
        }

        public static string StripLineBreaks(this string str)
        {
            return str.Replace("\n", " ").Replace("\r", " ");
        }

        private static string StopAtWordBreak(string str)
        {
            if (!str.Contains(' ')) return str;
            return str.Substring(0, str.LastIndexOf(' '));
        }

        private static string AppendString(string baseStr, string appendStr)
        {
            return string.Format("{0}{1}", baseStr, appendStr);
        }

        public static List<int> ToIntegers(this string str, char delimiter)
        {
				
            List<int> result = new List<int>();

            if (string.IsNullOrEmpty(str)) return new List<int>();

            List<string> substrs = str.Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (var s in substrs)
            {
                int i;
                if (int.TryParse(s, out i))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        public static int ToInteger(this string str)
        {
            return ToInteger(str, -1);
        }

        public static int ToInteger(this string str, int defaultValue)
        {
            int i;
            if (int.TryParse(str, out i)) return i;
            return defaultValue;
        }

        public static List<string> ToStrings(this string str, char delimiter)
        {
            if (string.IsNullOrEmpty(str)) return new List<string>();

            List<string> substrs = str.Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries).ToList();
            return substrs;
        }

        public static string ToFriendlyString(this string str)
        {
            if (string.IsNullOrEmpty(str)) return "";

			return Agility.Web.AgilityContentServer.DynamicPageFormulaItem.MakePathFriendly(str);
			
			/*
            str = Regex.Replace(str, @"&\w+;", "");
            str = Regex.Replace(str, @"[^a-z0-9\-\s]", "", RegexOptions.IgnoreCase);
            str = str.Replace(" ", "-");
            str = Regex.Replace(str, @"-{2,}", "-");
			 * 
			 */
			//return str;
        }

		public static string ToSentence(this List<string> str, string separator, string separatorLast)
		{
			if (str == null || str.Count == 0) return string.Empty;

			if (str.Count > 1)
			{
				return string.Format("{0}{1}{2}",
					string.Join(separator, str.Take(str.Count - 1).ToArray()),
					separatorLast, str.Last());
			}
			
			return str[0];
		}

		public static string EncodeToJsString(this string s)
		{
			if (string.IsNullOrEmpty(s)) return "\"\"";

			StringBuilder sb = new StringBuilder();
			sb.Append("\"");
			
			foreach (char c in s)
			{
				switch (c)
				{
					case '\"':
						sb.Append("\\\"");
						break;
					case '\\':
						sb.Append("\\\\");
						break;
					case '\b':
						sb.Append("\\b");
						break;
					case '\f':
						sb.Append("\\f");
						break;
					case '\n':
						sb.Append("\\n");
						break;
					case '\r':
						sb.Append("\\r");
						break;
					case '\t':
						sb.Append("\\t");
						break;
					default:
						int i = c;
						if (i < 32 || i > 127)
						{
							sb.AppendFormat("\\u{0:X04}", i);
						}
						else
						{
							sb.Append(c);
						}
						break;
				}
			}

			sb.Append("\"");
			return sb.ToString();
		}

        public static string Left(this string s, int length)
        {
            return s.Substring(0, Math.Min(s.Length, length));
        }
    }
}
