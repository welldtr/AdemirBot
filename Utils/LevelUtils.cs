using System.Text.RegularExpressions;

namespace DiscordBot.Utils
{
    public static class LevelUtils
    {
        public static int GetLevel(long xp)
        {
            int lvl;

            if (xp - 100 > 50)
            {
                lvl = Convert.ToInt32(Math.Floor(1 + Math.Sqrt((xp - 100) / 50)));
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
