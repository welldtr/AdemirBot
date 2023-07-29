using System.Text.RegularExpressions;

namespace DiscordBot.Utils
{
    public static class LevelUtils
    {
        public static int GetLevel(long messageCount)
        {
            int lvl;
            var xp = GetXPProgression(messageCount);

            if (xp - 100 > 50)
            {
                lvl = Convert.ToInt32(1 + Math.Sqrt((xp - 100) / 50));
            }
            else
            {
                lvl = 0;
            }
            return lvl;
        }

        public static long GetXPProgression(long messageCount)
        {
            return Convert.ToInt64((27.47 * messageCount) + 27.27); // Regressão linear feita para descobrir o padrão do Lurkr
        }
    }
}
