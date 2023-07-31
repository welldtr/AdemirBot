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

        public static async Task ImportLevelInfo(DiscordShardedClient client, IGuild guild, Context db)
        {
            int page = 1;
            var users = await guild.GetUsersAsync();

            while (true)
            {
                var result = await Lurkr.GetLurkrInfo(guild, page);
                if (result == null || !result.Levels.Any())
                    break;

                foreach (var info in result.Levels)
                {
                    var userId = ulong.Parse(info.UserId);
                    var user = client.GetUser(userId);
                    var member = await db.members.FindOneAsync(a => a.MemberId == userId && a.GuildId == guild.Id);

                    if (member == null)
                    {
                        member = new Member
                        {
                            Id = Guid.NewGuid(),
                            GuildId = guild.Id,
                            MemberId = userId,
                            MemberUserName = user?.Username,
                            MemberNickname = user?.GlobalName
                        };
                    }

                    member.MessageCount = info.MessageCount;
                    member.LurkrXP = info.XP;
                    member.LurkrLevel = info.Level;
                    member.XP = member.XP < member.LurkrXP ? member.LurkrXP : member.XP;
                    member.Level = LevelUtils.GetLevel(member.XP);
                    member.LastMessageTime = null;
                    await db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);
                }

                var config = await db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
                if(config == null)
                {
                    config = new AdemirConfig() {
                        GuildId = guild.Id
                    };
                }
                config.RoleRewards = result.RoleRewards;
                await db.ademirCfg.UpsertAsync(config, a => a.GuildId == config.GuildId);

                page++;
            }

        }
    }
}
