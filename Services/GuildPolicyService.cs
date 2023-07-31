using Discord;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Utils;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System.Diagnostics;

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
            var conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
            ConventionRegistry.Register("IgnoreExtraElements", conventionPack, type => true);
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

                    foreach (var guild in _client.Guilds)
                    {
                        try
                        {
                            //await Lurkr.ImportLevelInfo(_client, guild, _db);
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
                    }
                    await Task.Delay(TimeSpan.FromMinutes(15));
                }
            });

            var __ = Task.Run(async () =>
            {
                while (true)
                {
                    var sw = new Stopwatch();
                    var tasks = _client.Guilds.Select(guild => Task.Run(async () =>
                    {
                        try
                        {
                            foreach (var voice in guild.VoiceChannels)
                            {
                                if (voice.Id == guild.AFKChannel.Id)
                                    continue;

                                if (voice.ConnectedUsers.Where(a => !a.IsBot).Count() < 2)
                                    continue;

                                foreach (var user in voice.ConnectedUsers)
                                {
                                    if (user.IsMuted || user.IsDeafened)
                                        continue;

                                    var member = await _db.members.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == guild.Id);
                                    
                                    if (member == null)
                                    {
                                        member = Member.FromGuildUser(user);
                                    }

                                    if (user.IsSelfMuted || user.IsSelfDeafened)
                                    {
                                        _log.LogInformation($"+5xp de call: {member.MemberUserName}");
                                        member.XP += 5;
                                        member.MutedTime += TimeSpan.FromMinutes(1);
                                    }
                                    else
                                    {
                                        _log.LogInformation($"+10xp de call: {member.MemberUserName}");
                                        member.XP += 10;
                                        member.VoiceTime += TimeSpan.FromMinutes(1);
                                    }

                                    if (user.IsVideoing)
                                    {
                                        member.XP += 10;
                                        _log.LogInformation($"+10xp de camera: {member.MemberUserName}");
                                        member.VideoTime += TimeSpan.FromMinutes(1);
                                    }
                                    
                                    if (user.IsStreaming)
                                    {
                                        member.XP += 5;
                                        _log.LogInformation($"+5xp de streaming: {member.MemberUserName}");
                                        member.StreamingTime += TimeSpan.FromMinutes(1);
                                    }

                                    member.Level = LevelUtils.GetLevel(member.XP);
                                    await _db.members.UpsertAsync(member, a => a.MemberId == user.Id && a.GuildId == guild.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Erro ao apurar XP de audio");
                        }
                    })).ToArray();
                    Task.WaitAll(tasks);
                    await Task.Delay(TimeSpan.FromSeconds(60) - sw.Elapsed);
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
            var member = await _db.members.FindOneAsync(a => a.MemberId == arg.Id && a.GuildId == arg.Guild.Id);
            var _ = Task.Run(async () =>
            {
                await Task.Delay(10000);
                await ProcessRoleRewards(member);
            });
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

            await ProcessXPPerMessage(arg);
        }

        List<IMessage> mensagensUltimos5Minutos = new List<IMessage>();
        private async Task ProcessXPPerMessage(SocketMessage arg)
        {
            if (arg.Channel is IThreadChannel)
                return;

            if (!(arg is SocketUserMessage userMessage) || userMessage.Author == null)
                return;

            if (!arg.Author?.IsBot ?? false)
                mensagensUltimos5Minutos.Add(arg);

            var ppm = ProcessWPM();
            Console.Write($"PPM: {ppm}");

            var member = await _db.members.FindOneAsync(a => a.MemberId == arg.Author.Id && a.GuildId == arg.GetGuildId());
            var lastTime = member?.LastMessageTime ?? DateTime.MinValue;
            if (member == null)
            {
                member = Member.FromGuildUser(arg.Author as IGuildUser);
                member.MessageCount = 0;
            }

            var initialLevel = member.Level;

            var isCoolledDown = lastTime.AddSeconds(60) >= arg.Timestamp.UtcDateTime;

            if (isCoolledDown)
            {
                Console.WriteLine($"{arg.Author.Username} chill...");
                return;
            }

            member.MessageCount++;
            member.LastMessageTime = arg.Timestamp.UtcDateTime;

            var timeSinceCoolDown = arg.Timestamp.UtcDateTime - lastTime;
            var raidPpm = 50M;
            var ppmMax = ppm > raidPpm ? raidPpm : ppm;
            var gainReward = ((raidPpm - ppmMax) / raidPpm) * 25M;
            var earnedXp = (int)gainReward + 15;
            member.XP += earnedXp;
            member.Level = LevelUtils.GetLevel(member.XP);

            if (member.Level != initialLevel)
            {
                await ProcessRoleRewards(member);
            }
            await _db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);

            Console.WriteLine($"{arg.Author.Username} +{earnedXp} member xp -> {member.XP}");
        }

        public async Task ProcessRoleRewards(Member member)
        {
            var guild = _client.GetGuild(member.GuildId);
            var user = guild.GetUser(member.MemberId);
            var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == member.GuildId);

            if (config == null)
            {
                _log.LogError("Impossível processar recompensas de nivel. Configuração de level nao executada");
                return;
            }

            if (!config.EnableRoleRewards)
                return;

            var allRoleRewards = config.RoleRewards.SelectMany(a => a.Roles)
                .Where(a => user.Roles.Any(b => b.Id == ulong.Parse(a.Id)))
                .Select(a => ulong.Parse(a.Id));

            var levelRoles = config.RoleRewards
                .Where(a => a.Level < member.Level)
                .OrderByDescending(a => a.Level)
                .FirstOrDefault()?.Roles.Select(a => ulong.Parse(a.Id));

            if (levelRoles == null || levelRoles.Count() == 0)
                return;

            await user.RemoveRolesAsync(allRoleRewards);
            await user.AddRolesAsync(levelRoles);
        }

        private int ProcessWPM()
        {
            mensagensUltimos5Minutos = mensagensUltimos5Minutos.Where(a => a.Timestamp.UtcDateTime >= DateTime.UtcNow.AddMinutes(-5)).ToList();
            return mensagensUltimos5Minutos.Sum(a => a.Content.Split(new char[] { ' ', ',', ';', '.', '-', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length)/5;
        }
    }
}
