using Discord;

namespace DiscordBot.Utils
{
    public static class IInteractionContextExtensions
    {
        public static bool IsPremium(this IGuild guild)
        {
            var premiumGuilds = Environment.GetEnvironmentVariable("PremiumGuilds")!.Split(',');
            return premiumGuilds.Contains(guild.Id.ToString());
        }
    }
}
