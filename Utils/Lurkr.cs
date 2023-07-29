using Discord;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Domain.Lurkr;
using System.Net.Http.Json;

namespace DiscordBot.Utils
{
    public static class Lurkr
    {
        public static async Task<RoleReward[]?> GetRoleRewardsAsync(IGuild guild)
        {
            try
            {
                var url = $"https://api.lurkr.gg/levels/{guild.Id}?page=1";
                using var client = new HttpClient();
                var info = await client.GetFromJsonAsync<LevelInfo>(url);
                return info?.RoleRewards;
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                throw;
            }
        }

        public static async Task<LevelInfo?> GetLurkrInfo(IGuild guild, int page = 1)
        {
            try
            {
                var url = $"https://api.lurkr.gg/levels/{guild.Id}?page={page}";
                using var client = new HttpClient();
                var info = await client.GetFromJsonAsync<LevelInfo>(url);
                return info;
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                throw;
            }
        }


        public static async Task ImportLevelInfo(SocketGuild guild, Context db)
        {
            int page = 1;

            while (true)
            {
                var result = await Lurkr.GetLurkrInfo(guild, page);
                if (result == null || !result.Levels.Any())
                    break;

                foreach (var user in guild.Users)
                {
                    var levelInfo = result.Levels.Where(a => ulong.Parse(a.UserId) == user.Id).FirstOrDefault();
                    if (levelInfo == null)
                        continue;

                    var member = await db.members.FindOneAsync(a => a.MemberId == user.Id);

                    if (member == null)
                    {
                        member = Member.FromSocketUser(user);
                    }

                    long xpEarned = LevelUtils.GetXPProgression(levelInfo.MessageCount);
                    member.XP = xpEarned;

                    member.MessageCount = levelInfo.MessageCount;
                    member.LurkrXP = levelInfo.XP;
                    member.LurkrLevel = levelInfo.Level;
                    member.Level = LevelUtils.GetLevel(member.MessageCount);
                    member.LastMessageTime = DateTime.UtcNow;
                    await db.members.UpsertAsync(member);
                }

                page++;
            }

        }
    }
}
