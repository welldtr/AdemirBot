using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using OpenAI.Managers;
using OpenAI;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using MongoDB.Driver;
using System.Text;
using System.Text.RegularExpressions;
using Discord.Audio;
using YoutubeExplode;
using Microsoft.Extensions.DependencyInjection;
using DiscordBot.Domain.Entities;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using Discord.Interactions;
using DiscordBot.Modules;
using DiscordBot.Utils;
using System.Collections.Concurrent;
using YoutubeExplode.Videos;
using SpotifyExplode;
using YoutubeExplode.Exceptions;
using DiscordBot.Domain;
using SpotifyExplode.Users;

namespace DiscordBot
{
    internal class Program
    {
        private readonly IServiceProvider _serviceProvider;
        private DiscordShardedClient _client;
        private ILogger<Program> _log;
        private Context _db;
        private OpenAIService _openAI;
        private ConcurrentDictionary<ulong, IAudioClient> _audioClients;
        private ConcurrentDictionary<ulong, PlaybackState> _playerState;
        private ConcurrentDictionary<ulong, Video> _currentVideo;
        private ConcurrentDictionary<ulong, ConcurrentQueue<Video>> _videos;
        private ConcurrentDictionary<ulong, CancellationTokenSource> _cts;
        private YoutubeClient _youtubeClient;
        string? mongoServer = Environment.GetEnvironmentVariable("MongoServer");
        string? gptKey = Environment.GetEnvironmentVariable("ChatGPTKey");

        public Program()
        {
            _serviceProvider = CreateProvider();
            _audioClients = new ConcurrentDictionary<ulong, IAudioClient>();
            _currentVideo = new ConcurrentDictionary<ulong, Video>();
            _videos = new ConcurrentDictionary<ulong, ConcurrentQueue<Video>>();
            _playerState = new ConcurrentDictionary<ulong, PlaybackState>();
            _cts = new ConcurrentDictionary<ulong, CancellationTokenSource>();
        }

        private IServiceProvider CreateProvider()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            var commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
            });

            var openAI = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = gptKey!
            });

            var mongo = new MongoClient(mongoServer);
            var db = mongo.GetDatabase("ademir");

            var collection = new ServiceCollection()
               .AddSingleton(db)
               .AddSingleton(config)
               .AddSingleton(commands)
               .AddSingleton(openAI)
               .AddSingleton<DiscordShardedClient>()
               .AddSingleton<Context>()
               .AddLogging(b =>
               {
                   b.AddConsole();
                   b.SetMinimumLevel(LogLevel.Information);
               });

            return collection.BuildServiceProvider();
        }

        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            var provider = CreateProvider();
            var commands = provider.GetRequiredService<CommandService>();
            _openAI = provider.GetRequiredService<OpenAIService>();
            _db = provider.GetRequiredService<Context>();
            _client = provider.GetRequiredService<DiscordShardedClient>();
            _log = provider.GetRequiredService<ILogger<Program>>();
            _youtubeClient = new YoutubeClient();

            _client.MessageReceived += _client_MessageReceived;
            _client.UserLeft += _client_UserLeft;
            _client.UserJoined += _client_UserJoined;
            _client.ButtonExecuted += _client_ButtonExecuted;
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;

            _client.ShardReady += async (shard) =>
            {
                await _client.SetGameAsync($"tudo e todos [{shard.ShardId}]", type: ActivityType.Listening);
                _log.LogInformation($"Shard Number {shard.ShardId} is connected and ready!");

                try
                {
                    var _interactionService = new InteractionService(_client.Rest);

                    await _interactionService.AddModuleAsync<BanModule>(provider);
                    await _interactionService.AddModuleAsync<DallEModule>(provider);
                    await _interactionService.AddModuleAsync<DenounceModule>(provider);
                    await _interactionService.AddModuleAsync<InactiveUsersModule>(provider);
                    await _interactionService.AddModuleAsync<MacroModule>(provider);
                    await _interactionService.AddModuleAsync<MusicModule>(provider);

                    _interactionService.SlashCommandExecuted += SlashCommandExecuted;

                    _client.InteractionCreated += async (x) =>
                    {
                        var ctx = new ShardedInteractionContext(_client, x);
                        var _ = await Task.Run(async () => await _interactionService.ExecuteCommandAsync(ctx, _serviceProvider));
                    };

                    foreach (var guild in _client.Guilds)
                    {
                        _videos.TryAdd(guild.Id, new ConcurrentQueue<Video>());
                        _currentVideo.TryAdd(guild.Id, null);
                        _playerState[guild.Id] = PlaybackState.Stopped;
                        _cts[guild.Id] = null;
                    }
                }
                catch (HttpException exception)
                {
                    var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                    Console.WriteLine(json);
                }

                Console.WriteLine("Bot is connected!");
            };

            var token = Environment.GetEnvironmentVariable("AdemirAuth");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private async Task _client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState old, SocketVoiceState @new)
        {
            if (user.Id == _client.CurrentUser.Id && @new.VoiceChannel == null)
            {
                _playerState[old.VoiceChannel.Guild.Id] = PlaybackState.Stopped;
                _videos[old.VoiceChannel.Guild.Id].Clear();
                if (_cts != null && !_cts[old.VoiceChannel.Guild.Id].IsCancellationRequested)
                    _cts[old.VoiceChannel.Guild.Id].Cancel();
            }
        }

        async Task SlashCommandExecuted(SlashCommandInfo arg1, Discord.IInteractionContext arg2, Discord.Interactions.IResult arg3)
        {
            if (!arg3.IsSuccess)
            {
                switch (arg3.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        await arg2.Interaction.RespondAsync($"Unmet Precondition: {arg3.ErrorReason}", ephemeral: true);
                        break;
                    case InteractionCommandError.UnknownCommand:
                        await arg2.Interaction.RespondAsync("Unknown command", ephemeral: true);
                        break;
                    case InteractionCommandError.BadArgs:
                        await arg2.Interaction.RespondAsync("Invalid number or arguments", ephemeral: true);
                        break;
                    case InteractionCommandError.Exception:
                        await arg2.Interaction.RespondAsync($"Command exception: {arg3.ErrorReason}", ephemeral: true);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        await arg2.Interaction.RespondAsync("Command could not be executed", ephemeral: true);
                        break;
                    default:
                        break;
                }
            }
        }

        private Task _client_ButtonExecuted(SocketMessageComponent arg)
        {
            Task _;
            switch (arg.Data.CustomId)
            {
                case "stop-music":
                    _videos[arg.GuildId ?? 0].Clear();
                    _cts[arg.GuildId ?? 0]?.Cancel();
                    _ = Task.Run(async () => await arg.UpdateAsync(a =>
                    {
                        a.Components = null;
                    }));
                    break;

                case "skip-music":
                    _cts[arg.GuildId ?? 0]?.Cancel();
                    _ = Task.Run(async () => await arg.UpdateAsync(a =>
                    {
                        a.Components = null;
                    }));
                    break;

                case "download-music":
                    _ = Task.Run(async () =>
                    {
                        var video = _currentVideo[arg.GuildId ?? 0];
                        await arg.DeferLoadingAsync();
                        var sourceFilename = await _youtubeClient.ExtractAsync(video, CancellationToken.None);
                        var regexName = new Regex(@"[^a-zA-Z0-9_-]");
                        var fileName = regexName.Replace(video.Title, " ") + ".mp3";
                        var attachment = await FFmpeg.CreateMp3Attachment(sourceFilename, fileName);
                        await arg.User.SendFileAsync(attachment);
                        await arg.DeleteOriginalResponseAsync();
                        File.Delete(sourceFilename);
                        File.Delete(sourceFilename + ".mp3");
                    });
                    break;
            }
            return Task.CompletedTask;
        }

        private async Task _client_UserJoined(SocketGuildUser arg)
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
                        var buttonMessages = await guild.SystemChannel
                            .GetMessagesAsync(1000)
                            .Where(a => a.Any(b => b.Type == MessageType.GuildMemberJoin))
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
                }
                member.MemberUserName = user.Username;
                member.DateLeft = dateleft;
                await _db.memberships.UpsertAsync(member);
            }
        }

        private async Task _client_MessageReceived(SocketMessage arg)
        {
            var channel = ((SocketTextChannel)arg.Channel);


            if (!arg.Author?.IsBot ?? false)
                await _db.messagelog.UpsertAsync(new Message
                {
                    MessageId = arg.Id,
                    ChannelId = channel.Id,
                    GuildId = channel.Guild.Id,
                    MessageDate = arg.Timestamp.UtcDateTime,
                    UserId = arg.Author?.Id ?? 0,
                    MessageLength = arg.Content.Length
                });

            var guildId = channel.Guild.Id;
            var guild = _client.Guilds.First(a => a.Id == guildId);

            var user = guild.GetUser(arg.Author?.Id ?? 0);

            if (arg.Content.StartsWith(">pp"))
            {
                var query = arg.Content.Substring(4);
                var _ = Task.Run(async () => await PlayAudioCommand(channel, user, query));
            }

            if (user == null)
            {
                return;
            }

            try
            {
                if (guild.IsPremium())
                {
                    if ((arg.Channel as IThreadChannel) != null && ((IThreadChannel)arg.Channel).OwnerId == _client.CurrentUser.Id && arg.Author.Id != _client.CurrentUser.Id)
                    {
                        await ProcessarMensagemNoChatGPT(arg);
                    }
                    else if (arg.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id))
                    {
                        await ProcessarMensagemNoChatGPT(arg);
                    }
                    else if (arg.Reference != null && arg.Reference.MessageId.IsSpecified)
                    {
                        var msg = await arg.Channel.GetMessageAsync(arg.Reference.MessageId.Value!);
                        if (msg?.Author.Id == _client.CurrentUser.Id)
                        {
                            await ProcessarMensagemNoChatGPT(arg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }

            await VerificarSeMensagemDeBump(arg);
            if (user.GuildPermissions.Administrator && arg.Content.StartsWith("%") && arg.Content.Length > 1 && !arg.Content.Contains(' '))
            {
                var macro = await _db.macros
                    .FindOneAsync(a => a.GuildId == guildId && a.Nome == arg.Content.Substring(1));

                if (macro != null)
                {
                    await channel.SendMessageAsync(macro.Mensagem, allowedMentions: AllowedMentions.None);
                    await channel.DeleteMessageAsync(arg);
                }
            }
        }

        private async Task PlayAudioCommand(ITextChannel channel, IGuildUser user, string query)
        {
            var ademirConfig = await _db.ademirCfg.FindOneAsync(a => a.GuildId == channel.GuildId);
            IUserMessage msg = null;
            string sourceFilename = string.Empty;
            if (_cts[channel.GuildId]?.IsCancellationRequested ?? true)
                _cts[channel.GuildId] = new CancellationTokenSource();
            var token = _cts[channel.GuildId].Token;
            try
            {
                if (!query.Trim().StartsWith("http"))
                {
                    query = await Youtube.GetFirstVideoUrl(query);
                }

                Video video = null;
                if (query.Trim().StartsWith("https://open.spotify.com/"))
                {
                    var spotify = new SpotifyClient();

                    var regex = new Regex(@"https\:\/\/open\.spotify\.com\/(intl-\w+/)?(playlist|track|album)\/([a-zA-Z0-9]+)");

                    var type = regex.Match(query.Trim()).Groups[2].Value;
                    var id = regex.Match(query.Trim()).Groups[3].Value;
                    switch (type)
                    {
                        case "playlist":
                            var playlistTracks = await spotify.Playlists.GetAllTracksAsync(id);
                            var playListvideos = new Video[playlistTracks.Count];
                            var downloads = Enumerable.Range(0, playlistTracks.Count).Select(i => Task.Run(async () =>
                            {
                                var youtubeId2 = await spotify.Tracks.GetYoutubeIdAsync(playlistTracks[i].Url);
                                video = await _youtubeClient.Videos.GetAsync(VideoId.Parse(youtubeId2), token);
                                playListvideos[i] = video;
                            }));
                            Task.WaitAll(downloads.ToArray());

                            foreach (var v in playListvideos)
                                _videos[channel.GuildId].Enqueue(v);

                            await channel.SendMessageAsync($"", embed: new EmbedBuilder()
                               .WithTitle($"{playlistTracks.Count} musicas adicionadas à fila:")
                               .WithDescription($"{video.Title} - {video.Author} Duração: {video.Duration}")
                               .Build());
                            break;

                        case "album":
                            var albumTracks = await spotify.Albums.GetAllTracksAsync(id);
                            var videos = new Video[albumTracks.Count];
                            var albumDownloads = Enumerable.Range(0, albumTracks.Count).Select(i => Task.Run(async () =>
                            {
                                var youtubeId2 = await spotify.Tracks.GetYoutubeIdAsync(albumTracks[i].Url);
                                video = await _youtubeClient.Videos.GetAsync(VideoId.Parse(youtubeId2), token);
                                videos[i] = video;
                            }));
                            Task.WaitAll(albumDownloads.ToArray());

                            foreach (var v in videos)
                                _videos[channel.GuildId].Enqueue(v);

                            await channel.SendMessageAsync($"", embed: new EmbedBuilder()
                               .WithTitle($"{albumTracks.Count} musicas adicionadas à fila:")
                               .WithDescription($"{video.Title} - {video.Author} Duração: {video.Duration}")
                               .Build());
                            break;

                        case "track":
                            var youtubeId3 = await spotify.Tracks.GetYoutubeIdAsync(query.Trim());
                            video = await _youtubeClient.Videos.GetAsync(VideoId.Parse(youtubeId3), token);
                            break;
                    }
                }
                else
                {
                    video = await _youtubeClient.Videos.GetAsync(query, token);
                }

                if (_playerState[channel.GuildId] != PlaybackState.Stopped)
                {
                    await channel.SendMessageAsync($"", embed: new EmbedBuilder()
                               .WithTitle("Adicionada à fila:")
                               .WithDescription($"{video.Title} - {video.Author} Duração: {video.Duration}")
                               .Build());
                    _videos[channel.GuildId].Enqueue(video);
                    return;
                }

                _videos[channel.GuildId].Enqueue(video);

                var components = new ComponentBuilder()
                    .WithButton("Parar", "stop-music", ButtonStyle.Danger)
                    .WithButton("Avançar", "skip-music", ButtonStyle.Primary)
                    .WithButton("Baixar", "download-music", ButtonStyle.Success)
                    .Build();
                var voiceChannel = user.VoiceChannel;
                if (voiceChannel != null && !token.IsCancellationRequested)
                {
                    while (_videos[channel.GuildId].TryDequeue(out video))
                    {
                        _currentVideo[channel.GuildId] = video;
                        try
                        {
                            sourceFilename = await _youtubeClient.ExtractAsync(video, token);
                            var embed = new EmbedBuilder()
                               .WithColor(Color.Red)
                               .WithAuthor("Tocando Agora ♪")
                               .WithDescription($"[{video.Title}]({video.Url})\n`00:00 / {video.Duration:mm\\:ss}`")
                               .WithThumbnailUrl(video.Thumbnails.FirstOrDefault()?.Url)
                               .WithFields(new[] {
                           new EmbedFieldBuilder().WithName("Autor").WithValue(video.Author)
                               })
                               .Build();
                            msg = await channel.SendMessageAsync(embed: embed, components: components);
                        }
                        catch (VideoUnplayableException ex)
                        {
                            await channel.SendMessageAsync($"", embed: new EmbedBuilder()
                                       .WithTitle("Esta música não está disponível:")
                                       .WithDescription($"{video.Title} - {video.Author} Duração: {video.Duration}")
                                       .Build());
                            continue;
                        }

                        if (_audioClients.GetValueOrDefault(channel.GuildId)?.ConnectionState != ConnectionState.Connected)
                            _audioClients[channel.GuildId] = await voiceChannel.ConnectAsync(selfDeaf: true);

                        var volume = ademirConfig?.GlobalVolume ?? 100;

                        using (var ffmpeg = FFmpeg.CreateStream(sourceFilename, volume))
                        using (var output = ffmpeg?.StandardOutput.BaseStream)
                        using (var discord = _audioClients.GetValueOrDefault(channel.GuildId)?
                                                                .CreatePCMStream(AudioApplication.Music))
                        {
                            if (output == null)
                            {
                                _cts[channel.GuildId]?.Cancel();
                            }
                            else
                            {
                                try
                                {
                                    _playerState[channel.GuildId] = PlaybackState.Playing;
                                    await _audioClients.GetValueOrDefault(channel.GuildId)!.SetSpeakingAsync(true);
                                    await output.CopyToAsync(discord, token);
                                }
                                catch (OperationCanceledException)
                                {
                                    _cts[channel.GuildId] = new CancellationTokenSource();
                                    token = _cts[channel.GuildId].Token;
                                }
                                await msg.ModifyAsync(a => a.Components = new ComponentBuilder().Build());
                            }
                        }

                    }
                }

                _playerState[channel.GuildId] = PlaybackState.Stopped;
            }
            catch (OperationCanceledException)
            {
                _playerState[channel.GuildId] = PlaybackState.Stopped;
            }
            catch (Exception ex)
            {
                _playerState[channel.GuildId] = PlaybackState.Stopped;
                await channel.SendMessageAsync($"Erro ao tocar musica: {ex}");
            }
            finally
            {
                if (msg != null)
                {
                    await msg.ModifyAsync(a => a.Components = new ComponentBuilder().Build());
                }

                if (!string.IsNullOrEmpty(sourceFilename))
                {
                    File.Delete(sourceFilename);
                }

            }

            await channel.SendMessageAsync(embed: new EmbedBuilder()
               .WithTitle("Fila terminada")
               .Build());

            _playerState[channel.GuildId] = PlaybackState.Stopped;
            await Task.Delay(30000);

            if (token.IsCancellationRequested)
                if (_audioClients[channel.GuildId]?.ConnectionState == ConnectionState.Connected)
                    await _audioClients[channel.GuildId].StopAsync();
        }

        private async Task GetRepliedMessages(ITextChannel channel, IMessage message, List<ChatMessage> msgs)
        {
            var regexName = new Regex(@"[^a-zA-Z0-9_-]");
            var guild = _client.GetGuild(channel.GuildId);
            while (message.Reference != null && message.Reference.MessageId.IsSpecified)
            {
                if (channel.Id != message.Reference.ChannelId)
                    channel = (ITextChannel)guild.GetChannel(message.Reference.ChannelId);

                message = await channel.GetMessageAsync(message.Reference.MessageId.Value!);
                var me = guild.Users.First(a => a.Id == (message?.Author?.Id ?? 0));
                var autor = (me.Id == _client.CurrentUser.Id) ? "assistant" : "user";
                var nome = regexName.Replace(message.Author.Id == _client.CurrentUser.Id ? "Ademir" : me.DisplayName, "");
                if (message.Type == MessageType.Default)
                    msgs.Insert(0, new ChatMessage(autor, message.Content.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
            }
        }

        private async Task GetThreadMessages(IThreadChannel thread, IMessage message, List<ChatMessage> msgs)
        {
            var regexName = new Regex(@"[^a-zA-Z0-9_-]");
            var guild = _client.GetGuild(thread.GuildId);

            var msgsThread = await thread.GetMessagesAsync(message.Id, Direction.Before).FlattenAsync();
            foreach (var m in msgsThread)
            {
                var me = guild.Users.First(a => a.Id == (m?.Author?.Id ?? 0));
                var autor = (m.Id == _client.CurrentUser.Id ? "assistant" : "user");
                var nome = regexName.Replace(me.Id == _client.CurrentUser.Id ? "Ademir" : me.DisplayName, "");
                if(m.Type == MessageType.Default)
                    msgs.Insert(0, new ChatMessage(autor, m.Content.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
            }

            var firstMsg = msgsThread.LastOrDefault();
            var ch = (ITextChannel)guild.GetChannel(firstMsg!.Reference.ChannelId);
            await GetRepliedMessages(ch, firstMsg, msgs);
        }


        private async Task ProcessarMensagemNoChatGPT(SocketMessage arg)
        {
            var channel = (ITextChannel)arg.Channel;
            var channelId = channel.Id;
            var msgRefer = new MessageReference(arg.Id, channelId);
            var guild = ((SocketTextChannel)channel).Guild;
            var me = guild.Users.First(a => a.Id == arg.Author.Id);
            var ademirConfig = (await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id));
            var role = guild.Roles.FirstOrDefault(a => a.Id == (ademirConfig?.AdemirRoleId ?? 0));

            var isUserEnabled = me.PremiumSince.HasValue
                || me.GuildPermissions.Administrator
                || (role != null && me.Roles.Any(a => a.Id == role?.Id));

            if (!isUserEnabled)
            {
                if (role == null)
                    await channel.SendMessageAsync("Atualmente somente a staff e boosters podem falar comigo.", messageReference: msgRefer);
                else
                    await channel.SendMessageAsync($"Assine o cargo {role.Name} ou dê boost no servidor para falar comigo.", messageReference: msgRefer);

                return;
            }

            if (string.IsNullOrWhiteSpace(arg.Content.Replace($"<@{_client.CurrentUser.Id}>", "")))
            {
                if (arg.Author?.Id != _client.CurrentUser.Id)
                    await arg.AddReactionAsync(new Emoji("🥱"));
                return;
            }

            var regexName = new Regex(@"[^a-zA-Z0-9_-]");
            var attachmentContent = (arg.Attachments.Count == 0) ? "" : await new HttpClient().GetStringAsync(arg.Attachments.First(a => a.ContentType.StartsWith("text/plain")).Url);
            var content = (arg.Content + attachmentContent).Replace($"<@{_client.CurrentUser.Id}>", "Ademir");

            var m = (IMessage)arg;
            var msgs = new List<ChatMessage>() { new ChatMessage("user", content, regexName.Replace(me.DisplayName, "")) };

            await GetRepliedMessages(channel, m, msgs);

            if ((channel as IThreadChannel) != null)
            {
                msgRefer = null;
                await GetThreadMessages((channel as IThreadChannel)!, m, msgs);
            }

            StringBuilder chatString = new StringBuilder();
            foreach (var msg in msgs)
                chatString.AppendLine($"({msg.Name ?? "Regras"}) {msg.Content}");

            if ((channel as IThreadChannel) == null && msgs.Count == 2)
            {
                var result = await _openAI.Completions.CreateCompletion(
                new CompletionCreateRequest()
                {
                    Prompt = $"De acordo com o chat de discord abaixo:\n\n{chatString}\n\nCriar um nome de Tópico curto para esta conversa",
                    Model = Models.TextDavinciV1,
                    Temperature = 0.2F,
                    N = 1,
                });

                if (result.Successful)
                {
                    var titulo = result.Choices.First().Text.Replace(":", "").Trim();
                    channel = await channel.CreateThreadAsync(titulo, autoArchiveDuration: ThreadArchiveDuration.OneHour, message: arg);
                    msgRefer = null;
                }
            }

            var onlineUsers = guild.Users.Where(a => !a.IsBot && a.Status != UserStatus.Offline).Select(a => $" - {a.Nickname}");
            var bots = guild.Users.Where(a => a.IsBot).Select(a => $" - {a.Username}");
            var usersInCall = guild.Users.Where(a => a.VoiceChannel != null).Select(a => $" - {a.Nickname}");

            var onlineUsersSummary = string.Join(" \n", onlineUsers);
            var botsSummary = string.Join(" \n", bots);
            var usersInCallSummary = string.Join(" \n", usersInCall);

            var channels = guild.Channels
                .Where(a => a.GetPermissionOverwrite(guild.EveryoneRole).HasValue && a.GetPermissionOverwrite(guild.EveryoneRole)!.Value.ViewChannel != PermValue.Deny);

            await channel.TriggerTypingAsync();
            var completionResult = await _openAI.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest()
                {
                    Messages = msgs,
                    Model = Models.ChatGpt3_5Turbo,
                    Temperature = 0.2F,
                    N = 1,
                });

            if (completionResult.Successful)
            {
                foreach (var choice in completionResult.Choices)
                {
                    var resposta = choice.Message.Content;
                    var embeds = new List<Embed>();
                    try
                    {
                        var mm = await channel.SendMessageAsync(resposta, embeds: embeds.ToArray(), messageReference: msgRefer, allowedMentions: AllowedMentions.None);

                        if (choice.Message.Content.Contains(">>"))
                        {
                            var pedido = choice.Message.Content.Split("\n", StringSplitOptions.RemoveEmptyEntries)
                                .FirstOrDefault(a => a.Contains(">>"))?.Replace(">>", "");

                            var imageResult = await _openAI.Image.CreateImage(new ImageCreateRequest
                            {
                                Prompt = pedido!,
                                N = 1,
                                Size = StaticValues.ImageStatics.Size.Size512,
                                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                            });

                            resposta = resposta.Replace($">>{pedido}", "");

                            if (imageResult.Successful)
                            {
                                foreach (var img in imageResult.Results)
                                    embeds.Add(new EmbedBuilder().WithImageUrl(img.Url).Build());
                            }
                        }

                        await mm.ModifyAsync(m => m.Embeds = embeds.ToArray());
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            else
            {
                if (completionResult.Error?.Type == "insufficient_quota")
                {
                    await channel.SendMessageAsync("Desculpe. A cota de interações com o GPT excedeu, por conta disso estou sem cérebro.", messageReference: msgRefer);
                }

                else if (completionResult.Error == null)
                {
                    await channel.SendMessageAsync("Ocorreu um erro desconhecido", messageReference: msgRefer);
                }
                else
                {
                    await channel.SendMessageAsync($"Ocorreu um erro: ```{completionResult.Error?.Code}: {completionResult.Error?.Message}```", messageReference: msgRefer);
                    Console.WriteLine($"{completionResult.Error?.Code}: {completionResult.Error?.Message}");
                }
            }
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
                    await mentionedUser.SendMessageAsync($"Você ganhou {config.XPPerBump}xp por bumpar o servidor {guild.Name}");
                    Console.WriteLine($"{mentionedUser.Username} ganhou {config.XPPerBump}xp.");

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