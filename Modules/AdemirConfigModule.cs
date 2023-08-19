using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Entities;
using DiscordBot.Services;
using DiscordBot.Utils;
using MongoDB.Driver;

namespace DiscordBot.Modules
{
    public class AdemirConfigModule : InteractionModuleBase
    {
        private readonly Context db;
        private readonly GuildPolicyService policySvc;

        public AdemirConfigModule(Context context, GuildPolicyService policySvc)
        {
            db = context;
            this.policySvc = policySvc;
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
        [SlashCommand("set-activetalker-role", "Configurar cargo de participação ativa.")]
        public async Task SetActiveTalkerRole(
            [Summary(description: "Cargo de participação ativa")] IRole cargo)
        {
            var config = (await db.ademirCfg.FindOneAsync(a => a.GuildId == Context.Guild.Id));
            if (config == null)
            {
                await db.ademirCfg.AddAsync(new AdemirConfig
                {
                    AdemirConfigId = Guid.NewGuid(),
                    GuildId = Context.Guild.Id,
                    ActiveTalkerRole = cargo.Id
                });
            }
            else
            {
                config.ActiveTalkerRole = cargo.Id;
                await db.ademirCfg.UpsertAsync(config);
            }

            await RespondAsync("Cargo de participação ativa configurado.", ephemeral: true);
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("lock-server", "Bloquear a entrada de novos membros")]
        public async Task Lock()
        {
            policySvc.LockServer(Context.Guild.Id);
            await RespondAsync("Entrada de novos membros bloqueada.", ephemeral: true);
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("unlock-server", "Desbloquear a entrada de novos membros")]
        public async Task UnLock()
        {
            policySvc.UnlockServer(Context.Guild.Id);
            await RespondAsync("Entrada de novos membros desbloqueada.", ephemeral: true);
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("welcomebanner", "Define a imagem do banner de boas vindas")]
        public async Task BackgroundSet([Summary(description: "Imagem (1600x400)")] IAttachment imagem = null)
        {
            await DeferAsync();
            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();

            if (cfg == null)
            {
                return;
            }

            if (imagem.ContentType.Matches("image/.*"))
            {
                using var client = new HttpClient();
                using var ms = new MemoryStream();
                var info = await client.GetStreamAsync(imagem.Url);
                info.CopyTo(ms);
                ms.Position = 0;
                cfg.WelcomeBanner = ms.ToArray();
            }
            else if (imagem == null)
            {
                cfg.WelcomeBanner = null;
            }
            else
            {
                await ModifyOriginalResponseAsync(a =>
                {
                    a.Content = $"A cor que você selecionou não é válida. Tente um numero hexadecimal, media ou cargo.";
                });
                return;
            }

            await db.ademirCfg.UpsertAsync(cfg, a => a.GuildId == Context.Guild.Id);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = " ";
                a.Embed = new EmbedBuilder()
                    .WithColor(Color.Default)
                    .WithCurrentTimestamp()
                    .WithDescription($"Imagem de boas vindas {(imagem == null ? "removida" : "definida")} com sucesso.")
                    .Build();
            });
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
