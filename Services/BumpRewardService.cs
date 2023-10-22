
using Discord;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services
{
    public class BumpRewardService : Service
    {
        private Context _db;
        private DiscordShardedClient _client;
        private ILogger<BumpRewardService> _log;

        public BumpRewardService(Context context, DiscordShardedClient client, ILogger<BumpRewardService> logger)
        {
            _db = context;
            _client = client;
            _log = logger;
        }

        public override void Activate()
        {
            BindEventListeners();
        }

        private void BindEventListeners()
        {
            _client.MessageReceived += _client_MessageReceived;
            _client.ShardReady += _client_ShardReady;
        }

        private Task _client_ShardReady(DiscordSocketClient arg)
        {
            var _ = Task.Run(async () =>
            {
                while (true)
                {
                    foreach (var guild in _client.Guilds)
                    {
                        await VerificarSeHoraDoBump(guild);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            });

            return Task.CompletedTask;
        }

        private async Task VerificarSeHoraDoBump(SocketGuild guild)
        {
            var config = (await _db.bumpCfg.FindOneAsync(a => a.GuildId == guild.Id));

            if (config == null)
                return;

            if((config.LastRemindTime == null || DateTime.UtcNow - config.LastRemindTime >= TimeSpan.FromMinutes(30)) 
                && DateTime.UtcNow - config.LastBumpTime >= TimeSpan.FromMinutes(120))
            {
                var canal = guild.GetTextChannel(config.BumpChannelId);

                await canal.SendMessageAsync($"<@&{config.BumpRoleId}>", 
                    embed: new EmbedBuilder()
                    .WithTitle("Ta na hora do BUMP!")
                    .WithDescription("Impulsione nosso servidor digitando `/bump` !")
                    .WithCurrentTimestamp()
                    .WithColor(new Color(0x875cfd))
                    .Build());

                config.LastRemindTime = DateTime.UtcNow;
                await _db.bumpCfg.UpsertAsync(config, a => a.GuildId == guild.Id);
            }
        }

        private Task _client_MessageReceived(SocketMessage arg)
        {
            var _ = Task.Run(() => VerificarSeMensagemDeBump(arg));
            return Task.CompletedTask;
        }

        private async Task VerificarSeMensagemDeBump(SocketMessage arg)
        {
            var guildId = ((SocketTextChannel)arg.Channel).Guild.Id;
            var guild = _client.Guilds.First(a => a.Id == guildId);
            var config = (await _db.bumpCfg.FindOneAsync(a => a.GuildId == guildId));

            if (config == null)
            {
                return;
            }

            var canal = (IMessageChannel)guild.Channels.First(a => a.Id == config.BumpChannelId);

            if (arg.Channel.Id == config.BumpChannelId &&
               (arg.Interaction?.Type == InteractionType.ApplicationCommand && arg.Interaction.Name == "bump"))
            {
                var bumper = arg.Interaction.User;

                await canal.SendMessageAsync($"Obrigado {bumper.Mention}. Você ganhou +{config.XPPerBump}xp por bumpar o servidor {guild.Name}.\n__**Atenção!** Para ganhar seu novo XP é necessário recepcionar os próximos novatos.__");
                _log.LogInformation($"{bumper.Username} ganhou {config.XPPerBump}xp.");

                var member = await _db.members.FindOneAsync(a => a.MemberId == bumper.Id && a.GuildId == guildId);
                config.LastBumpTime = arg.Timestamp.UtcDateTime;
                config.LastRemindTime = null;
                await _db.bumpCfg.UpsertAsync(config, a => a.GuildId == guildId);

                if (member != null)
                {
                    // member.XP += config.XPPerBump;
                    member.BumpCount++;
                    await _db.members.UpsertAsync(member, a => a.MemberId == bumper.Id && a.GuildId == guildId);
                    await _db.bumps.AddAsync(new Bump
                    {
                        BumpId = Guid.NewGuid(),
                        BumpDate = arg.Timestamp.DateTime,
                        GuildId = guildId,
                        UserId = bumper.Id,
                        XP = config.XPPerBump
                    });
                }
            }
        }
    }
}
