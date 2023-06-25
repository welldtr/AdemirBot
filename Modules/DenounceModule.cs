using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Entities;
using MongoDB.Driver;

namespace DiscordBot.Modules
{
    public class DenounceModule : InteractionModuleBase
    {
        private readonly DiscordShardedClient _client;
        private readonly Context db;

        public DenounceModule(DiscordShardedClient client, Context context)
        {
            _client = client;
            db = context;
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("config-denuncias", "Configurar o canal de denúncias.")]
        public async Task ConfigDenuncias(
            [Summary(description: "Canal de denúncias")] IChannel canal)
        {
            var admin = _client.Guilds.First(a => a.Id == Context.Guild.Id)
                .GetUser(Context.User.Id).GuildPermissions.Administrator;

            if (!admin)
            {
                await RespondAsync("Apenas administradores podem configurar o canal de denuncias.", ephemeral: true);
                return;
            }

            var config = await db.denunciaCfg.GetByIdAsync(Context.Guild.Id);
            if (config == null)
            {
                await db.denunciaCfg.AddAsync(new DenunciaConfig
                {
                    DenunciaId = Guid.NewGuid(),
                    GuildId = Context.Guild.Id,
                    ChannelId = canal.Id
                });
            }
            else
            {
                config.ChannelId = canal.Id;
                await db.denunciaCfg.UpsertAsync(config);
            }

            await RespondAsync("Canal de denúncias configurado.", ephemeral: true);
        }

        [SlashCommand("denunciar", "Denunciar algo que fere as regras do servidor.")]
        public async Task Denunciar(
            [Summary(description: "Usuário a denunciar")] IUser usuario,
            [Summary(description: "Relato da denuncia")] string relato,
            [Summary(description: "Testemunha")] IUser testemunha = default,
            [Summary(description: "Print da conversa")] IAttachment print = default,
            [Summary(description: "Postar anonimamente")] bool anonimato = default)
        {
            var config = await db.denunciaCfg.GetByIdAsync(Context.Guild.Id);

            if (config == null)
            {
                await RespondAsync($"O canal de denúncias ainda não está configurado.", ephemeral: true);
                return;
            }
            await RespondAsync($"Não se preocupe, {Context.User.Username}. Esta informação será mantida em sigilo.", ephemeral: true);

            var guildId = Context.Guild.Id;
            var guild = _client.Guilds.First(a => a.Id == guildId);
            var canal = (IMessageChannel)guild.Channels.First(a => a.Id == config.ChannelId);

            var denunciado = usuario;


            var msg = await canal.SendMessageAsync("", false, new EmbedBuilder()
            {
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName("Denunciado").WithIsInline(true).WithValue($"{denunciado}"),
                    new EmbedFieldBuilder().WithName("Denúncia").WithValue(relato),
                },
                ImageUrl = print?.Url,
                Description = anonimato ? "" : $"|| Denúnciado por: {Context.User.Mention} " + (testemunha == null ? "" : $"Testemunha: {testemunha.Mention}") + " ||"
            }.Build());

            await db.denuncias.AddAsync(new Denuncia
            {
                DenunciaId = Guid.NewGuid(),
                ReportId = msg.Id,
                GuildId = guildId,
                DenunciaDate = DateTime.Now,
                DenunciadoUserId = denunciado.Id,
                DenuncianteUserId = Context.User.Id,
                PrintUrl = print?.Url,
                TestemunhaUserId = testemunha?.Id
            });
        }
    }
}
