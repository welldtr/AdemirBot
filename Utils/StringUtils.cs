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
            var r = new Regex(@"[^a-zA-Z0-9_-]");
            return r.Replace(entrada, string.Empty);
        }
    }
}
