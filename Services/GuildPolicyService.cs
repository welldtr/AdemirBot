using Discord;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Utils;
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
        private Dictionary<ulong, List<string>> backlistPatterns = new Dictionary<ulong, List<string>>();
        List<IMessage> mensagensUltimos5Minutos = new List<IMessage>();

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
            _client.ReactionAdded += _client_ReactionAdded;
            _client.GuildMemberUpdated += _client_GuildMemberUpdated;
            _client.UserBanned += _client_UserBanned;
            _client.UserUnbanned += _client_UserUnbanned;
        }

        private async Task _client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> olduser, SocketGuildUser user)
        {
            var _ = Task.Run(async () =>
            {
                var member = await _db.members.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == user.Guild.Id);
                var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == user.Guild.Id);
                await CheckIfMinorsAndBanEm(config, member);
            });
        }

        private async Task _client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
        {
            var message = await _db.messagelog.FindOneAsync(a => a.MessageId == arg1.Id && a.ChannelId == arg2.Id);
            if (message != null)
            {
                var reactionkey = arg3.Emote.ToString()!;
                message.Reactions = message.Reactions ?? new Dictionary<string, int>();
                if (message.Reactions.ContainsKey(reactionkey))
                    message.Reactions[reactionkey]++;
                else
                    message.Reactions.Add(reactionkey, 1);
                await _db.messagelog.UpsertAsync(message);
            }
        }

        private async Task _client_ShardReady(DiscordSocketClient arg)
        {
            var _ = Task.Run(async () =>
            {
                while (true)
                {
                    foreach (var guild in _client.Guilds)
                    {
                        await SairDeServidoresNaoAutorizados(guild);
                        await ProcessMemberProgression(guild);
                        await TrancarThreadAntigasDoAdemir(guild);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(20));
                }
            });

            var __ = Task.Run(async () =>
            {
                while (true)
                {
                    var sw = new Stopwatch();
                    var tasks = _client.Guilds.Select(guild => Task.Run(async () =>
                    {
                        await ProcessarXPDeAudio(guild);
                        await BuscarPadroesBlacklistados(guild);
                    })).ToArray();
                    Task.WaitAll(tasks);
                    await Task.Delay(TimeSpan.FromSeconds(120) - sw.Elapsed);
                }
            });
        }

        private async Task SairDeServidoresNaoAutorizados(SocketGuild guild)
        {
            var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
            if (config == null || !config.Premium)
                await guild.LeaveAsync();
        }

        public async Task BuscarPadroesBlacklistados(IGuild guild)
        {
            var blacklist = await _db.backlistPatterns.Find(a => a.GuildId == guild.Id).ToListAsync();
            if(backlistPatterns.ContainsKey(guild.Id))
            {
                backlistPatterns[guild.Id] = blacklist.Select(a => a.Pattern).ToList();
            }
            else
            {
                backlistPatterns.Add(guild.Id, blacklist.Select(a => a.Pattern).ToList());
            }
        }

        private async Task ProcessarXPDeAudio(SocketGuild guild)
        {
            try
            {
                var events = await guild.GetEventsAsync();
                foreach (var voice in guild.VoiceChannels)
                {
                    var @event = events.FirstOrDefault(a => a.ChannelId == voice.Id && a.Status == GuildScheduledEventStatus.Active);
                    var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
                    if (voice.Id == guild.AFKChannel?.Id)
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
                            Console.WriteLine($"+2xp de call: {member.MemberUserName}");
                            earnedXp += 2;
                            member.MutedTime += TimeSpan.FromMinutes(2);
                        }
                        else
                        {
                            Console.WriteLine($"+5xp de call: {member.MemberUserName}");
                            earnedXp += 5;
                            member.VoiceTime += TimeSpan.FromMinutes(2);
                        }

                        if (user.IsVideoing)
                        {
                            earnedXp += 7;
                            Console.WriteLine($"+7xp de camera: {member.MemberUserName}");
                            member.VideoTime += TimeSpan.FromMinutes(2);
                        }

                        if (user.IsStreaming)
                        {
                            earnedXp += 2;
                            Console.WriteLine($"+2xp de streaming: {member.MemberUserName}");
                            member.StreamingTime += TimeSpan.FromMinutes(2);
                        }

                        if (@event != null)
                        {
                            var presence = await _db.eventPresence.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == guild.Id && a.EventId == @event.Id);

                            if (presence == null)
                            {
                                presence = new EventPresence
                                {
                                    EventPresenceId = Guid.NewGuid(),
                                    GuildId = guild.Id,
                                    MemberId = member.MemberId,
                                    EventId = @event.Id,
                                    ConnectedTime = TimeSpan.Zero
                                };
                                member.EventsPresent++;
                            }
                            presence.ConnectedTime += TimeSpan.FromMinutes(2);
                            await _db.eventPresence.UpsertAsync(presence, a => a.MemberId == user.Id && a.GuildId == guild.Id && a.EventId == @event.Id);

                            earnedXp *= 4;
                        }

                        var qtdPessoasEntraramNaMesmaEpoca = voice.ConnectedUsers.Where(a => ((a.JoinedAt - user.JoinedAt) ?? TimeSpan.Zero).Duration() <= TimeSpan.FromDays(21)).Count();
                        var outrasPessoas = voice.Users.Count - qtdPessoasEntraramNaMesmaEpoca;
                        if (qtdPessoasEntraramNaMesmaEpoca > 2)
                        {
                            earnedXp /= qtdPessoasEntraramNaMesmaEpoca;

                            Console.WriteLine($"dividido por {qtdPessoasEntraramNaMesmaEpoca}: {member.MemberUserName}");
                        }
                        member.XP += earnedXp;

                        Console.WriteLine($"{member.MemberUserName} +{earnedXp} member xp -> {member.XP}");
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
        }

        private async Task TrancarThreadAntigasDoAdemir(SocketGuild guild)
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
        }

        private async Task ProcessMemberProgression(SocketGuild guild)
        {
            try
            {
                var progression = await _db.progression.Find(t => t.GuildId == guild.Id && t.Date == DateTime.Today).FirstOrDefaultAsync();
                var joinsToday = await _db.memberships.Find(t => t.GuildId == guild.Id && t.DateJoined >= DateTime.Today).CountDocumentsAsync();
                var leftToday = await _db.memberships.Find(t => t.GuildId == guild.Id && t.DateLeft >= DateTime.Today).CountDocumentsAsync();

                if (progression == null)
                {
                    progression = new ServerNumberProgression
                    {
                        ServerNumberProgressionId = Guid.NewGuid(),
                        GuildId = guild.Id,
                        Date = DateTime.Today
                    };
                }
                progression.GrowthToday = joinsToday - leftToday;
                progression.MemberCount = guild.MemberCount;

                await _db.progression.UpsertAsync(progression);
                _log.LogInformation($"Membros em {guild.Name}: {guild.MemberCount}. Owner: {guild.Owner.Username}.");
            }
            catch
            {
                _log.LogError($"Erro ao registrar quantidade de membros do server {guild.Name}.");
            }
        }

        private async Task _client_MessageReceived(SocketMessage arg)
        {
            var _ = Task.Run(async() => await ProtectFromFloodAndBlacklisted(arg));
            var __ = Task.Run(async() => await LogMessage(arg));
        }

        private async Task ProtectFromFloodAndBlacklisted(SocketMessage arg)
        {
            if (!arg.Author?.IsBot ?? false)
                mensagensUltimos5Minutos.Add(arg);

            if (arg.Author != null)
            {
                var mensagensUltimos5Segundos = mensagensUltimos5Minutos.Where(a => a.Author.Id == arg.Author.Id && a.Timestamp.UtcDateTime >= DateTime.UtcNow.AddSeconds(-3));
                if (mensagensUltimos5Segundos.Count() > 3)
                {
                    var mensagensUltimos10Segundos = mensagensUltimos5Minutos.Where(a => a.Author.Id == arg.Author.Id && a.Timestamp.UtcDateTime >= DateTime.UtcNow.AddSeconds(-10));
                    var delecoes = mensagensUltimos10Segundos
                        .Select(async(msg) => await arg.Channel.DeleteMessageAsync(msg.Id))
                        .ToArray();

                    Task.WaitAll(delecoes);
                }
                else if (arg.Content.Matches(@"\S{80}") && arg.Content.Distinct().Count() < 9)
                {
                    await (arg.Channel as ITextChannel)!.DeleteMessageAsync(arg);
                }
                else if (backlistPatterns[arg.GetGuildId()].Any(a => arg.Content.Matches(a)))
                {
                    await (arg.Channel as ITextChannel)!.DeleteMessageAsync(arg);
                }
            }
        }

        private async Task _client_UserJoined(SocketGuildUser user)
        {
            var guild = _client.GetGuild(user.Guild.Id);
            var member = await _db.members.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == user.Guild.Id);
            if (member == null)
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
                await CheckIfMinorsAndBanEm(config, member);
            });

            await ProcessMemberProgression(guild);
        }

        private async Task GiveAutoRole(AdemirConfig config, SocketGuildUser user)
        {
            try
            {
                var role = user.Guild.GetRole(config.AutoRoleId);
                if (role != null)
                {
                    await user.AddRoleAsync(role);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"Erro ao definir cargo automatico para {user.Username}.");
            }
        }

        private async Task _client_UserBanned(SocketUser user, SocketGuild guild)
        {
            var member = await _db.members.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == guild.Id);
            var ban = await guild.GetBanAsync(user);
            member.DateBanned = DateTime.UtcNow;
            member.ReasonBanned = ban.Reason;
            await _db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);
        }

        private async Task _client_UserUnbanned(SocketUser user, SocketGuild guild)
        {
            var member = await _db.members.FindOneAsync(a => a.MemberId == user.Id && a.GuildId == guild.Id);
            member.DateBanned = null;
            member.ReasonBanned = null;
            await _db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);
        }

        private async Task CheckIfMinorsAndBanEm(AdemirConfig config, Member member)
        {
            try
            {
                var guild = _client.GetGuild(config.GuildId);
                var role = guild.GetRole(config.MinorRoleId);
                var user = guild.GetUser(member.MemberId);
                if (role != null)
                {
                    if (user.Roles.Any(a => a.Id == role.Id))
                    {
                        await user.SendMessageAsync("Oi. Tudo bem? Infelizmente não podemos aceitar menores de idade no nosso grupo. Desculpe.");
                        await user.BanAsync(0, "Menor de Idade");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"Erro ao expulsar o menor de idade: {member.MemberUserName}.");
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

            await ProcessMemberProgression(guild);
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
                    _log.LogError(ex, $"Erro ao apagar mensagem de boas vindas para: {member.MemberUserName}");
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
                    MessageLength = arg.Content.Length,
                    Reactions = arg.Reactions.ToDictionary(a => a.Key.ToString()!, b => b.Value.ReactionCount)
                });

            if (arg is IThreadChannel && ((IThreadChannel)arg).OwnerId == _client.CurrentUser.Id)
                await _db.threads.UpsertAsync(new ThreadChannel
                {
                    ThreadId = channel.Id,
                    GuildId = channel.Guild.Id,
                    MemberId = arg.Author?.Id ?? 0,
                    LastMessageTime = arg.Timestamp.UtcDateTime,
                });

            var ppm = ProcessWPM();
            Console.WriteLine($"PPM: {ppm}");
            await ProcessXPPerMessage(ppm, arg);
        }

        private async Task ProcessXPPerMessage(int ppm, SocketMessage arg)
        {
            if (arg.Channel is IThreadChannel)
                return;

            if (!(arg is SocketUserMessage userMessage) || userMessage.Author == null)
                return;

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
            var guild = _client.GetGuild(arg.GetGuildId());

            var config = await _db.ademirCfg.FindOneAsync(a => a.GuildId == member.GuildId);
            config.ChannelXpMultipliers = config.ChannelXpMultipliers ?? new Dictionary<ulong, double>();

            if (config.ChannelXpMultipliers.ContainsKey(arg.Channel.Id))
            {
                earnedXp *= (int)config.ChannelXpMultipliers[arg.Channel.Id];
            }

            member.XP += earnedXp;
            member.Level = LevelUtils.GetLevel(member.XP);

            await ProcessRoleRewards(config, member);

            await _db.members.UpsertAsync(member, a => a.MemberId == member.MemberId && a.GuildId == member.GuildId);

            Console.WriteLine($"{arg.Author?.Username} +{earnedXp} member xp -> {member.XP}");
        }

        public async Task ProcessRoleRewards(AdemirConfig config, Member member)
        {
            var guild = _client.GetGuild(member.GuildId);
            var user = guild.GetUser(member.MemberId);

            if (config == null || user == null)
            {
                _log.LogError("Impossível processar recompensas de nivel. Configuração de level nao executada");
                return;
            }

            if (!config.EnableRoleRewards)
                return;

            if (user.IsBot && user.Id != _client.CurrentUser.Id)
            {
                _log.LogError("Dos bots, só o Ademir pode ganhar XP.");
                return;
            }

            var levelRolesToAdd = config.RoleRewards
                .Where(a => a.Level <= member.Level)
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
