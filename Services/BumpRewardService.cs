
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
        }

        private async Task _client_MessageReceived(SocketMessage arg)
        {
            await VerificarSeMensagemDeBump(arg);
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
                arg.Content.Contains(config.BumpMessageContent!) &&
                arg.Author.Id == config.BumpBotId)
            {
                foreach (var mentionedUser in arg.MentionedUsers)
                {
                    await canal.SendMessageAsync($"Obrigado {mentionedUser.Mention}. Você ganhou +{config.XPPerBump}xp por bumpar o servidor {guild.Name}. Para manter seu novo XP, recepcione os novatos.");
                    _log.LogInformation($"{mentionedUser.Username} ganhou {config.XPPerBump}xp.");

                    var member = await _db.members.FindOneAsync(a => a.MemberId == arg.Author.Id && a.GuildId == guildId);

                    if (member != null)
                    {
                        member.XP += config.XPPerBump;
                        member.BumpCount++;
                        await _db.members.UpsertAsync(member, a => a.MemberId == arg.Author.Id && a.GuildId == guildId);
                        await _db.bumps.AddAsync(new Bump
                        {
                            BumpId = Guid.NewGuid(),
                            BumpDate = arg.Timestamp.DateTime,
                            GuildId = guildId,
                            UserId = mentionedUser.Id,
                            XP = config.XPPerBump
                        });
                    }
                }
            }
        }
    }
}
