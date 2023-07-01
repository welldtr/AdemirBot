using Discord;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Services
{
    public class GuildPolicyService : Service
    {
        private Context _db;
        private DiscordShardedClient _client;
        private ILogger<GuildPolicyService> _log;

        public GuildPolicyService(Context context, DiscordShardedClient client, ILogger<GuildPolicyService> logger)
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
            _client.UserJoined += _client_UserJoined;
            _client.UserLeft += _client_UserLeft;
        }

        private async Task _client_MessageReceived(SocketMessage arg)
        {
            await VerificarSeMacro(arg);
        }

        private async Task _client_UserJoined(SocketGuildUser arg)
        {
            await IncluirMembroNovo(arg);
        }
        private async Task _client_UserLeft(SocketGuild guild, SocketUser user)
        {
            var userId = user.Id;
            var guildId = guild.Id;
            var member = (await _db.memberships.FindOneAsync(a => a.MemberId == userId && a.GuildId == guildId));

            var dateleft = DateTime.UtcNow;
            if (member == null)
            {
                await _db.memberships.AddAsync(new Membership
                {
                    MembershipId = Guid.NewGuid(),
                    GuildId = guildId,
                    MemberId = userId,
                    MemberUserName = user.Username,
                    DateLeft = dateleft
                });
            }
            else
            {
                if (member.DateJoined != null)
                {
                    var tempoNoServidor = dateleft - member.DateJoined.Value;
                    if (tempoNoServidor < TimeSpan.FromMinutes(30))
                    {
                        await ProcurarEApagarMensagemDeBoasVindas(guild, member, member.DateJoined.Value);
                    }
                }
                member.MemberUserName = user.Username;
                member.DateLeft = dateleft;
                await _db.memberships.UpsertAsync(member);
            }
        }

        private async Task ProcurarEApagarMensagemDeBoasVindas(SocketGuild guild, Membership member, DateTime untilDate)
        {
            var buttonMessages = await guild.SystemChannel
                            .GetMessagesAsync(500)
                            .Where(a => a.Any(b => b.Type == MessageType.GuildMemberJoin && b.Author.Id == member.MemberId))
                            .Select(a => a.Where(b => b.Type == MessageType.GuildMemberJoin))
                            .FlattenAsync();

            foreach (var buttonMessage in buttonMessages)
            {
                try
                {
                    await guild.SystemChannel.DeleteMessageAsync(buttonMessage.Id);
                    Console.WriteLine($"Mensagem de boas vindas do usuario [{member.MemberUserName}] apagada.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        private async Task IncluirMembroNovo(SocketGuildUser arg)
        {
            var userId = arg.Id;

            var datejoined = arg.JoinedAt.HasValue ? arg.JoinedAt.Value.DateTime : default;

            await _db.memberships.AddAsync(new Membership
            {
                GuildId = arg.Guild.Id,
                MemberId = userId,
                MemberUserName = arg.Username,
                DateJoined = datejoined
            });
        }

        private async Task VerificarSeMacro(SocketMessage arg)
        {
            var channel = arg.GetTextChannel();
            var user = await arg.GetAuthorGuildUserAsync();
            if (user.GuildPermissions.Administrator && arg.Content.StartsWith("%") && arg.Content.Length > 1 && !arg.Content.Contains(' '))
            {
                var macro = await _db.macros
                    .FindOneAsync(a => a.GuildId == arg.GetGuildId() && a.Nome == arg.Content.Substring(1));

                if (macro != null)
                {
                    await channel.SendMessageAsync(macro.Mensagem, allowedMentions: AllowedMentions.None);
                    await channel.DeleteMessageAsync(arg);
                }
            }
        }
    }
}
