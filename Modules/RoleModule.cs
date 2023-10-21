using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Entities;
using MongoDB.Driver;

namespace DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.Administrator)]
    [Group("role", "Commandos de Cargos")]
    public class RoleModule : InteractionModuleBase
    {
        private readonly Context db;

        public RoleModule(Context context)
        {
            db = context;
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("add-level-reward", "Configura um cargo de level", runMode: RunMode.Async)]
        public async Task AddLevelReward(
            [Summary(description: "Cargo a adicionar")] IRole cargo,
            [Summary(description: "Level necessário")] int level)
        {
            await DeferAsync();

            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new AdemirConfig
                {
                    AdemirConfigId = Guid.NewGuid(),
                    GuildId = Context.Guild.Id,
                    RoleRewards = new Domain.Lurkr.RoleReward[0]
                };
            }

            if (cfg.RoleRewards.Any(a => a.Level == level))
            {
                var levelRecord = cfg.RoleRewards.FirstOrDefault(a => a.Level == level);

                if (levelRecord != null)
                    levelRecord.Roles.Append(new Domain.Lurkr.Role
                    {
                        Color = cargo.Color.ToString(),
                        Id = cargo.Id.ToString(),
                        Name = cargo.Name,
                        Position = cargo.Position,
                    });
            }
            else
            {
                cfg.RoleRewards.Append(new Domain.Lurkr.RoleReward
                {
                    Level = level,
                    Roles = new[] {
                        new Domain.Lurkr.Role {
                            Color = cargo.Color.ToString(),
                            Id = cargo.Id.ToString(),
                            Name = cargo.Name,
                            Position = cargo.Position,
                        }
                    }
                });
            }

            await db.ademirCfg.UpsertAsync(cfg, a => a.GuildId == Context.Guild.Id);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = $"Cargo **{cargo.Name}** adicionado para o nivel **{level}**.";
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("remove-level-reward", "Remove cargo de level", runMode: RunMode.Async)]
        public async Task RemoveLevelReward(
            [Summary(description: "Cargo a remover")] IRole cargo, int level = 0)
        {
            await DeferAsync();

            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new AdemirConfig
                {
                    AdemirConfigId = Guid.NewGuid(),
                    GuildId = Context.Guild.Id,
                    RoleRewards = new Domain.Lurkr.RoleReward[0]
                };
            }

            if (level == 0)
            {
                cfg.RoleRewards = cfg.RoleRewards.Select(a => new Domain.Lurkr.RoleReward
                {
                    Level = a.Level,
                    Roles = a.Roles.Where(a => a.Id != cargo.Id.ToString()).ToArray()
                }).ToArray();
            }
            else
            {
                var levelRecord = cfg.RoleRewards.FirstOrDefault(a => a.Level == level);

                if (levelRecord != null)
                    levelRecord.Roles = levelRecord.Roles.Where(a => a.Id != cargo.Id.ToString()).ToArray();
            }

            await db.ademirCfg.UpsertAsync(cfg, a => a.GuildId == Context.Guild.Id);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = $"Cargo **{cargo.Name}** removido dos cargos de nível.";
            });
        }
    }
}
