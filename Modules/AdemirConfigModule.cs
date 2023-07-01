using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Entities;

namespace DiscordBot.Modules
{
    public class AdemirConfigModule : InteractionModuleBase
    {
        private readonly Context db;

        public AdemirConfigModule(Context context)
        {
            db = context;
        }
        
        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("config-cargo-ademir", "Configurar cargo que pode falar com o Ademir.")]
        public async Task ConfigCargoAdemir(
            [Summary(description: "Cargo permitido falar com o Ademir")] IRole cargo)
        {
            var config = (await db.ademirCfg.FindOneAsync(a => a.GuildId == Context.Guild.Id));
            if (config == null)
            {
                await db.ademirCfg.AddAsync(new AdemirConfig
                {
                    AdemirConfigId = Guid.NewGuid(),
                    GuildId = Context.Guild.Id,
                    AdemirRoleId = cargo.Id
                });
            }
            else
            {
                config.AdemirRoleId = cargo.Id;
                await db.ademirCfg.UpsertAsync(config);
            }

            await RespondAsync("Cargo permitido para o Ademir configurado.", ephemeral: true);
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("config-rewards", "Configurar recompensas de bump.")]
        public async Task ConfigRewards(
            [Summary(description: "Canal do Bump")] IChannel canal,
            [Summary(description: "Bot reminder")] IUser bot,
            [Summary(description: "Conteudo da mensagem")] string conteudo,
            [Summary(description: "XP por bump")] long xp)
        {
            var config = (await db.bumpCfg.FindOneAsync(a => a.GuildId == Context.Guild.Id));

            if (config == null)
            {
                config = new BumpConfig
                {
                    BumpConfigId = Guid.NewGuid(),
                    GuildId = Context.Guild.Id,
                    BumpChannelId = canal.Id,
                    BumpBotId = bot.Id,
                    BumpMessageContent = conteudo,
                    XPPerBump = (int)xp
                };
                await db.bumpCfg.AddAsync(config);
            }
            else
            {
                config.BumpChannelId = canal.Id;
                config.BumpBotId = bot.Id;
                config.BumpMessageContent = conteudo;
                config.XPPerBump = (int)xp;
                await db.bumpCfg.UpsertAsync(config);
            }
            await RespondAsync("Recompensas por bump configuradas.", ephemeral: true);
        }
    }
}
