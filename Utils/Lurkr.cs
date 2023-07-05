using Discord;
using DiscordBot.Domain.Lurkr;
using System.Net.Http.Json;

namespace DiscordBot.Utils
{
    public static class Lurkr
    {
        public static async Task<RoleReward[]?> GetRoleRewardsAsync(IGuild guild)
        {
            var url = $"https://api.lurkr.gg/levels/{guild.Id}?page=1";
            using var client = new HttpClient();
            var info  = await client.GetFromJsonAsync<LevelInfo>(url);
            return info?.RoleRewards;
        }
    }
}
