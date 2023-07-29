using Discord;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Utils;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

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
            _client.ShardReady += _client_ShardReady;
        }

        private async Task _client_ShardReady(DiscordSocketClient arg)
        {
            var _ = Task.Run(async () =>
            {
                while (true)
                {
                    var conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
                    ConventionRegistry.Register("IgnoreExtraElements", conventionPack, type => true);

                    foreach (var guild in _client.Guilds)
                    {
                        try
                        {
                            var threads = await _db.threads.Find(t => t.LastMessageTime >= DateTime.UtcNow.AddHours(-72) && t.LastMessageTime <= DateTime.UtcNow.AddHours(-12)).ToListAsync();

                            foreach (var thread in threads)
                            {
                                var threadCh = guild.GetThreadChannel(thread.ThreadId);
                                if (threadCh != null)
                                    await threadCh.ModifyAsync(a => a.Archived = true);
                            }
                        }
                        catch
                        {
                            _log.LogError("Erro ao trancar threads do Ademir.");
                        }

                        try
                        {
                            await Lurkr.ImportLevelInfo(guild, _db);
                            _log.LogInformation($"Importação de levels do Lurkr no server {guild.Name} concluída.");
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Erro ao importar levels do Lurkr");
                        }
                    }
                    await Task.Delay(TimeSpan.FromMinutes(15));
                }
            });
        }


        private async Task _client_MessageReceived(SocketMessage arg)
        {
            await LogMessage(arg);
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

        private async Task LogMessage(SocketMessage arg)
        {
            var channel = ((SocketTextChannel)arg.Channel);

            if (!arg.Author?.IsBot ?? false)
                await _db.messagelog.UpsertAsync(new Message
                {
                    MessageId = arg.Id,
                    ChannelId = channel.Id,
                    GuildId = channel.Guild.Id,
                    Content = arg.Content,
                    MessageDate = arg.Timestamp.UtcDateTime,
                    UserId = arg.Author?.Id ?? 0,
                    MessageLength = arg.Content.Length
                });

            if (arg is IThreadChannel && ((IThreadChannel)arg).OwnerId == _client.CurrentUser.Id)
                await _db.threads.UpsertAsync(new ThreadChannel
                {
                    ThreadId = channel.Id,
                    GuildId = channel.Guild.Id,
                    MemberId = arg.Author?.Id ?? 0,
                    LastMessageTime = arg.Timestamp.UtcDateTime,
                });

            await ProcessXP(arg);
        }

        private async Task ProcessXP(SocketMessage arg)
        {
            var (messageCount, lastMessage) = await RaiseAndGetMsgCount(arg);

            var isCoolledDown = lastMessage.AddMinutes(1) >= arg.Timestamp.UtcDateTime;

            if (isCoolledDown)
            {
                Console.WriteLine($"{arg.Author.Username} chill...");
                return;
            }

            if (!(arg is SocketUserMessage userMessage) || userMessage.Author == null)
                return;

            var member = await _db.members.FindOneAsync(a => a.MemberId == arg.Author.Id && a.GuildId == arg.GetGuildId());
            member.MessageCount = messageCount;
            member.XP = LevelUtils.GetXPProgression(member.MessageCount);
            member.Level = LevelUtils.GetLevel(member.XP);
            await _db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);

            Console.WriteLine($"{arg.Author.Username} + member xp: {member.XP}");
        }

        private async Task<(long, DateTime)> RaiseAndGetMsgCount(SocketMessage arg)
        {
            if (arg.Author == null)
                return (0, DateTime.Now);

            var member = await _db.members.FindOneAsync(a => a.MemberId == arg.Author.Id && a.GuildId == arg.GetGuildId());
            var lastTime = member.LastMessageTime;
            if (member == null)
            {
                member = Member.FromGuildUser(arg.Author as IGuildUser);
                member.MessageCount = 0;
            }

            member.MessageCount++;
            member.LastMessageTime = arg.Timestamp.UtcDateTime;
            await _db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);
            return (member.MessageCount, lastTime);
        }
    }
}
