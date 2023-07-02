using System.Text.RegularExpressions;

namespace DiscordBot.Utils
{
    public static class StringUtils
    {
        public static ulong[] SplitAndParseMemberIds(string memberIds)
        {
            return memberIds
                .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => ulong.Parse(a))
                .ToArray();
        }

        public static string AsAlphanumeric(this string entrada)
        {
            return entrada.RegexReplace(@"[^a-zA-Z0-9_-]", string.Empty);
        }

        public static bool Matches(this string entrada, string pattern)
        {
            var r = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return r.IsMatch(entrada);
        }

        public static Match Match(this string entrada, string pattern)
        {
            var r = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return r.Match(entrada);
        }

        public static string RegexReplace(this string entrada, string pattern, string replacement)
        {
            var r = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return r.Replace(entrada, replacement);
        }
    }
}
