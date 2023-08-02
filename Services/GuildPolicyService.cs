using Amazon.Runtime.Internal.Util;
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
                            // await Lurkr.ImportLevelInfo(_client, guild, _db);
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
                            var events = await guild.GetEventsAsync();
                            foreach (var voice in guild.VoiceChannels)
                            {
                                var @event = events.FirstOrDefault(a => a.ChannelId == voice.Id && a.Status == GuildScheduledEventStatus.Active);
                                var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
                                if (voice.Id == guild.AFKChannel.Id)
                                    continue;

                                if (voice.ConnectedUsers.Where(a => !a.IsBot).Count() < 2)
                                    continue;

                                foreach (var user in voice.ConnectedUsers)
                                {
                                    if (user.IsMuted || user.IsDeafened)
                                        continue;

                                    var member = await _db.members.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == guild.Id);
                                    var initialLevel = member.Level;
                                    int earnedXp = 0;
                                    if (member == null)
                                    {
                                        member = Member.FromGuildUser(user);
                                    }

                                    if (user.IsSelfMuted || user.IsSelfDeafened)
                                    {
                                        _log.LogInformation($"+2xp de call: {member.MemberUserName}");
                                        earnedXp += 2;
                                        member.MutedTime += TimeSpan.FromMinutes(2);
                                    }
                                    else
                                    {
                                        _log.LogInformation($"+5xp de call: {member.MemberUserName}");
                                        earnedXp += 10;
                                        member.VoiceTime += TimeSpan.FromMinutes(2);
                                    }

                                    if (user.IsVideoing)
                                    {
                                        earnedXp += 10;
                                        _log.LogInformation($"+7xp de camera: {member.MemberUserName}");
                                        member.VideoTime += TimeSpan.FromMinutes(2);
                                    }

                                    if (user.IsStreaming)
                                    {
                                        earnedXp += 5;
                                        _log.LogInformation($"+2xp de streaming: {member.MemberUserName}");
                                        member.StreamingTime += TimeSpan.FromMinutes(2);
                                    }

                                    if(@event != null)
                                    {
                                        earnedXp *= 2;
                                    }

                                    member.XP = earnedXp;
                                    member.Level = LevelUtils.GetLevel(member.XP);
                                    await _db.members.UpsertAsync(member, a => a.MemberId == user.Id && a.GuildId == guild.Id);

                                    await ProcessRoleRewards(config, member);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Erro ao apurar XP de audio");
                        }
                    })).ToArray();
                    Task.WaitAll(tasks);
                    await Task.Delay(TimeSpan.FromSeconds(120) - sw.Elapsed);
                }
            });
        }


        private async Task _client_MessageReceived(SocketMessage arg)
        {
            await LogMessage(arg);
        }

        private async Task _client_UserJoined(SocketGuildUser user)
        {
            var member = await _db.members.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == user.Guild.Id);
            if(member == null)
            {
                member = Member.FromGuildUser(user);
                await _db.members.AddAsync(member);
            }

            await IncluirMembroNovo(user);
            var _ = Task.Run(async () =>
            {
                var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == member.GuildId);
                await GiveAutoRole(config, user);
                await Task.Delay(3000);
                await ProcessRoleRewards(config, member);
                await CheckIfMinorsAndKickEm(config, user);
            });
        }

        private async Task GiveAutoRole(AdemirConfig config, SocketGuildUser user)
        {
            var role = user.Guild.GetRole(config.AutoRoleId);
            if (role != null)
            {
                await user.AddRoleAsync(role);
            }
        }

        private async Task CheckIfMinorsAndKickEm(AdemirConfig config, SocketGuildUser user)
        {
            var role = user.Guild.GetRole(config.MinorRoleId);
            if (role != null)
            {
                if(user.Roles.Any(a => a.Id == role.Id))
                    await user.KickAsync("Menor de Idade");
            }
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
            Console.WriteLine($"PPM: {ppm}");

            var member = await _db.members.FindOneAsync(a => a.MemberId == arg.Author!.Id && a.GuildId == arg.GetGuildId());
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
                Console.WriteLine($"{arg.Author?.Username} chill...");
                return;
            }

            member.MessageCount++;
            member.LastMessageTime = arg.Timestamp.UtcDateTime;

            var timeSinceCoolDown = arg.Timestamp.UtcDateTime - lastTime;
            var raidPpm = 120M;
            var ppmMax = ppm > raidPpm ? raidPpm : ppm;
            var gainReward = ((raidPpm - ppmMax) / raidPpm) * 25M;
            var earnedXp = (int)gainReward + 15;
            member.XP += earnedXp;
            member.Level = LevelUtils.GetLevel(member.XP);

            var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == member.GuildId);
            await ProcessRoleRewards(config, member);
            await _db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);

            Console.WriteLine($"{arg.Author?.Username} +{earnedXp} member xp -> {member.XP}");
        }

        public async Task ProcessRoleRewards(AdemirConfig config, Member member)
        {
            var guild = _client.GetGuild(member.GuildId);
            var user = guild.GetUser(member.MemberId);

            if (config == null)
            {
                _log.LogError("Impossível processar recompensas de nivel. Configuração de level nao executada");
                return;
            }

            if (!config.EnableRoleRewards)
                return;

            var levelRolesToAdd = config.RoleRewards
                .Where(a => a.Level < member.Level)
                .OrderByDescending(a => a.Level)
                .FirstOrDefault()?.Roles.Select(a => ulong.Parse(a.Id)) ?? new ulong[] { };

            var levelRolesToRemove = config.RoleRewards.SelectMany(a => a.Roles)
                .Where(a => user.Roles.Any(b => b.Id == ulong.Parse(a.Id)) 
                        && !levelRolesToAdd.Any(b => b == ulong.Parse(a.Id)))
                .Select(a => ulong.Parse(a.Id));

            if (levelRolesToAdd.Count() == 0)
                return;

            await user.AddRolesAsync(levelRolesToAdd);
            await user.RemoveRolesAsync(levelRolesToRemove);
        }

        private int ProcessWPM()
        {
            mensagensUltimos5Minutos = mensagensUltimos5Minutos.Where(a => a.Timestamp.UtcDateTime >= DateTime.UtcNow.AddMinutes(-5)).ToList();
            return mensagensUltimos5Minutos.Sum(a => a.Content.Split(new char[] { ' ', ',', ';', '.', '-', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length) / 5;
        }
    }
}
