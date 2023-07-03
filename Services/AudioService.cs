using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Domain.ValueObjects;
using DiscordBot.Utils;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Exceptions;

namespace DiscordBot.Services
{
    public class AudioService : Service
    {
        private Context _db;
        private DiscordShardedClient _client;
        private ILogger<AudioService> _log;
        private ConcurrentDictionary<ulong, IAudioClient> _audioClients;
        private ConcurrentDictionary<ulong, PlaybackState> _playerState;
        private ConcurrentDictionary<ulong, float> _decorrido;
        private ConcurrentDictionary<ulong, Track> _currentTrack;
        private ConcurrentDictionary<ulong, ConcurrentQueue<Track>> _tracks;
        private ConcurrentDictionary<ulong, CancellationTokenSource> _cts;
        private ConcurrentDictionary<ulong, int> _volume;
        private ConcurrentDictionary<ulong, Func<TimeSpan, Task>> _positionFunc;
        private Dictionary<string, Emote> emote;

        public AudioService(Context context, DiscordShardedClient client, ILogger<AudioService> logger)
        {
            _db = context;
            _client = client;
            _log = logger;
        }

        public override void Activate()
        {
            InitializeDictionaries();
            BindEventListeners();
        }

        private void BindEventListeners()
        {
            _client.ShardReady += _client_ShardReady;
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;
            _client.MessageReceived += _client_MessageReceived;
        }

        private Task _client_MessageReceived(SocketMessage arg)
        {
            var guildId = ((SocketTextChannel)arg.Channel).Guild.Id;
            var guild = _client.Guilds.First(a => a.Id == guildId);
            var channel = ((SocketTextChannel)arg.Channel);
            var user = guild.GetUser(arg.Author?.Id ?? 0);

            if (user == null)
            {
                return Task.CompletedTask;
            }

            Task _;
            switch (arg.Content)
            {
                case ">>skip":
                    _ = Task.Run(async () => await SkipMusic(guildId));
                    break;

                case ">>pause":
                    _ = Task.Run(async () => await PauseMusic(guildId));
                    break;

                case ">>stop":
                    _ = Task.Run(async () => await StopMusic(guildId));
                    break;

                case ">>quit":
                    _ = Task.Run(async () => await QuitVoice(guildId));
                    break;

                case ">>queue":
                    _ = Task.Run(async () => await ShowQueue(channel));
                    break;

                case ">>loop":
                    break;

                case ">>clear":
                    break;

                case ">>replay":
                    break;

                case ">>loopqueue":
                    break;

                case ">>shuffle":
                    break;

                case string s when s.Matches(@">>volume (\d+)"):
                    var volumestr = arg.Content.Match(@">>volume (\d+)").Groups[1].Value;
                    var volume = int.Parse(volumestr);
                    _ = Task.Run(async () => await SetVolume(guildId, volume));
                    break;

                case string s when s.StartsWith(">>"):
                    var query = arg.Content.Substring(2);
                    _ = Task.Run(async () => await PlayMusic(channel, user, query));
                    break;
            }

            return Task.CompletedTask;
        }

        private async Task ShowQueue(ITextChannel channel)
        {
            var plStr = new StringBuilder();
            int i = 0;
            string line = "+---+".PadRight(36, '-') + "+";
            foreach (var track in _tracks[channel.GuildId])
            {
                i++;
                var position = $"#{i}".PadLeft(3);
                var title = track.Title.PadRight(31).Substring(0, 31);
                var author = track.Author.PadRight(23).Substring(0, 23);

                var trackstring = $"{line}\n|{position}|{title}|\n|   |{author} [{track.Duration:mm\\:ss}]|";

                if (i > 20)
                    break;

                plStr.AppendLine(trackstring);
            }
            if (_tracks[channel.GuildId].Count == 0)
            {
                await channel.SendEmbedText("Esta é a ultima música da fila no momento.");
            }
            else
            {
                await channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Próximas músicas {(_tracks[channel.GuildId].Count > 20 ? "(20 faixas)" : "")}:")
                    .WithDescription($"```{line}\n{plStr}{line}```")
                    .Build());
            }
        }

        private void InitializeDictionaries()
        {
            _audioClients = new ConcurrentDictionary<ulong, IAudioClient>();
            _positionFunc = new ConcurrentDictionary<ulong, Func<TimeSpan, Task>>();
            _decorrido = new ConcurrentDictionary<ulong, float>();
            _currentTrack = new ConcurrentDictionary<ulong, Track>();
            _tracks = new ConcurrentDictionary<ulong, ConcurrentQueue<Track>>();
            _playerState = new ConcurrentDictionary<ulong, PlaybackState>();
            _cts = new ConcurrentDictionary<ulong, CancellationTokenSource>();
            _volume = new ConcurrentDictionary<ulong, int>();
        }

        private async Task _client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState old, SocketVoiceState @new)
        {
            if (user.Id == _client.CurrentUser.Id && @new.VoiceChannel == null)
            {
                _playerState[old.VoiceChannel.Guild.Id] = PlaybackState.Stopped;
                _tracks[old.VoiceChannel.Guild.Id].Clear();
            }
        }

        private async Task _client_ShardReady(DiscordSocketClient client)
        {
            emote = new Dictionary<string, Emote>()
            {
                {"stop", Emote.Parse("<:stop:1123770944784179210>") },
                {"play", Emote.Parse("<:play:1123770947984437259>") },
                {"pause", Emote.Parse("<:pause:1123770941235794033>") },
                {"skip", Emote.Parse("<:skip:1123771732243787887>") },
                {"download", Emote.Parse("<:download:1123771345667358720>") },
            };

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            foreach (var guild in client.Guilds)
            {
                try
                {
                    var ademirConfig = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
                    _tracks.TryAdd(guild.Id, new ConcurrentQueue<Track>());
                    _currentTrack.TryAdd(guild.Id, null);
                    _playerState[guild.Id] = PlaybackState.Stopped;
                    _cts[guild.Id] = null;
                    _volume[guild.Id] = ademirConfig?.GlobalVolume ?? 100;
                    _positionFunc[guild.Id] = (a) => Task.CompletedTask;
                    _decorrido[guild.Id] = 0;

                    if (ademirConfig?.VoiceChannel != null && (ademirConfig?.PlaybackState ?? PlaybackState.Stopped) != PlaybackState.Stopped)
                    {
                        guild.GetVoiceChannel(ademirConfig.VoiceChannel.Value!);

                        var tracks = await _db.tracks.Find(a => a.GuildId == guild.Id)
                                            .SortBy(a => a.QueuePosition)
                                            .ToListAsync();

                        var track = tracks.FirstOrDefault();
                        if (track != null)
                        {
                            var channel = guild.GetTextChannel(track.ChannelId);
                            var user = guild.GetUser(track.UserId);
                            _decorrido[guild.Id] = ademirConfig.Position ?? 0;
                            _playerState[guild.Id] = ademirConfig.PlaybackState!;
                            var _ = Task.Run(() => PlayMusic(channel, user, tracks: tracks.ToArray()));
                        }
                    }
                }
                catch(Exception ex)
                {
                    _log.LogError(ex, "Erro ao inicializar serviço de musica");
                }
            }
            ExecuteTrackPositionLoop();
        }

        private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            var tasks = _client.Guilds.Select(guild => Task.Run(async () => await SavePlaybackInfo(guild))).ToArray();
            Task.WaitAll(tasks);
        }

        private async Task SavePlaybackstate(SocketGuild guild)
        {
            var ademirConfig = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
            ademirConfig.PlaybackState = _playerState[guild.Id];
            ademirConfig.Position = _decorrido[guild.Id];
            ademirConfig.VoiceChannel = guild.CurrentUser.VoiceChannel?.Id;
            ademirConfig.PlaybackState = _playerState[guild.Id];
            await _db.ademirCfg.UpsertAsync(ademirConfig);
        }

        private async Task SavePlaybackInfo(SocketGuild guild)
        {
            await SavePlaybackstate(guild);
            await SavePlaylistInfo(guild);
        }

        private async Task SavePlaylistInfo(SocketGuild guild)
        {
            ConcurrentQueue<Track> tracks = new ConcurrentQueue<Track>();
            for (int i = 0; i < 5; i++)
                if (_tracks.TryGetValue(guild.Id, out tracks))
                    break;

            await _db.tracks.DeleteAsync(a => a.GuildId == guild.Id);
            int position = 1;

            if (_currentTrack[guild.Id] != null)
            {
                var track = _currentTrack[guild.Id];
                track.QueuePosition = position;
                await _db.tracks.AddAsync(track);
            }
            foreach (var track in tracks)
            {
                position++;
                track.QueuePosition = position;
                await _db.tracks.AddAsync(track);
            }
        }

        private void ExecuteTrackPositionLoop()
        {
            var _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                while (true)
                {
                    try
                    {
                        foreach (var guild in _client.Guilds)
                        {
                            await _positionFunc[guild.Id](TimeSpan.FromSeconds(_decorrido[guild.Id]));
                            await SavePlaybackInfo(guild);
                        }
                    }
                    catch (Exception) {; }
                    await Task.Delay(1000);
                }
            });

            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                while (true)
                {
                    try
                    {
                        foreach (var guild in _client.Guilds)
                        {
                            await SavePlaylistInfo(guild);
                        }
                    }
                    catch (Exception) {; }
                    await Task.Delay(5000);
                }
            });
        }

        private async Task EnqueueTracks(IGuildUser user, ITextChannel channel, Track[] tracks)
        {
            if (tracks.Length > 1)
                await channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithFooter($"{tracks.Length} músicas adicionadas à fila por {user.DisplayName}", user.GetDisplayAvatarUrl())
                    .Build());

            else if (tracks.Length > 0 && _tracks[channel.GuildId].Count > 0)
                await channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor("Adicionada à fila:")
                    .WithDescription($"[{tracks[0].Title}]({tracks[0].Url})\n`00:00 / {tracks[0].Duration:mm\\:ss}`")
                    .WithFooter($"Adicionada por {user.DisplayName}", user.GetDisplayAvatarUrl())
                    .Build());

            foreach (var track in tracks)
            {
                track.UserId = user.Id;
                track.AppendDate = DateTime.Now;
                track.GuildId = channel.GuildId;
                track.ChannelId = channel.Id;
                _tracks[channel.GuildId].Enqueue(track);
            }
        }

        public async Task SetVolume(ulong guildId, int volume)
        {
            var cfg = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guildId);

            if (cfg == null)
            {
                cfg = new AdemirConfig
                {
                    GuildId = guildId,
                    GlobalVolume = volume
                };
            }
            else
            {
                cfg.GlobalVolume = volume;
            }

            await _db.ademirCfg.UpsertAsync(cfg);

            await Task.Run(() =>
            {
                for (int i = 0; i < 5; i++)
                    if (_volume.TryUpdate(guildId, volume, _volume[guildId]))
                        break;
            });
        }

        public Task PauseMusic(ulong guildId)
        {
            _playerState[guildId] = _playerState[guildId] == PlaybackState.Playing ? PlaybackState.Paused : PlaybackState.Playing;
            return Task.CompletedTask;
        }

        public Task StopMusic(ulong guildId)
        {
            _tracks[guildId].Clear();
            _cts[guildId]?.Cancel();
            return Task.CompletedTask;
        }

        public Task SkipMusic(ulong guildId)
        {
            _cts[guildId]?.Cancel();
            _cts[guildId] = new CancellationTokenSource();
            return Task.CompletedTask;
        }

        public async Task QuitVoice(ulong guildId)
        {
            await _audioClients[guildId].StopAsync();
        }

        public async Task DownloadAtachment(SocketMessageComponent arg)
        {
            var youtubeClient = new YoutubeClient();
            var video = _currentTrack[arg.GuildId ?? 0];
            await arg.DeferLoadingAsync();
            var sourceFilename = await youtubeClient.ExtractAsync(video, CancellationToken.None);
            var fileName = video.Title.AsAlphanumeric() + ".mp3";
            var attachment = await FFmpeg.CreateMp3Attachment(sourceFilename, fileName);
            await arg.User.SendFileAsync(attachment);
            await arg.DeleteOriginalResponseAsync();
            File.Delete(sourceFilename);
            File.Delete(sourceFilename + ".mp3");
        }

        public async Task PlayMusic(ITextChannel channel, IGuildUser user, string query = null, params Track[] tracks)
        {
            IUserMessage msg = null;
            var guild = _client.GetGuild(channel.GuildId);
            string sourceFilename = string.Empty;

            if (_cts[channel.GuildId]?.IsCancellationRequested ?? true)
                _cts[channel.GuildId] = new CancellationTokenSource();

            var token = _cts[channel.GuildId].Token;
            try
            {
                var voiceChannel = user.VoiceChannel;

                if (voiceChannel == null)
                {
                    await channel.SendMessageAsync(embed: new EmbedBuilder()
                               .WithTitle("Você precisa estar em um canal de voz.")
                               .Build());
                    return;
                }

                if (query == null)
                {
                    await EnqueueTracks(user, channel, tracks);
                }
                else
                {
                    await ResolveQuery(user, query, channel);
                    await SavePlaylistInfo(guild);
                }

                bool resume = query == null;

                if (!resume && _playerState[channel.GuildId] != PlaybackState.Stopped)
                {
                    return;
                }

                var components = GetAudioControls(PlaybackState.Playing);

                if (voiceChannel != null && !token.IsCancellationRequested)
                {
                    while (_tracks[channel.GuildId].TryDequeue(out Track track) && track != null)
                    {
                        await SavePlaylistInfo(guild);
                        _positionFunc[channel.GuildId] = (a) => Task.CompletedTask;
                        var queuedBy = await channel.Guild.GetUserAsync(track.UserId);
                        var banner = PlayerBanner(track, queuedBy);
                        _currentTrack[channel.GuildId] = track;

                        try
                        {
                            sourceFilename = await new YoutubeClient().ExtractAsync(track, token);
                            msg = await channel.SendMessageAsync(embed: banner.Build(), components: components);
                        }
                        catch (VideoUnplayableException ex)
                        {
                            await channel.SendEmbedText(
                                "Esta música não está disponível:",
                                $"{track.Title} - {track.Author} Duração: {track.Duration:mm\\:ss}");
                            continue;
                        }

                        if (_audioClients.GetValueOrDefault(channel.GuildId)?.ConnectionState != ConnectionState.Connected)
                            _audioClients[channel.GuildId] = await voiceChannel.ConnectAsync(selfDeaf: true);

                        var start = TimeSpan.Zero;
                        if (resume)
                        {
                            start = TimeSpan.FromSeconds(_decorrido[channel.GuildId]);
                            resume = false;
                        }

                        using (var ffmpeg = FFmpeg.CreateStream(sourceFilename, start))
                        using (var output = ffmpeg?.StandardOutput.BaseStream)
                        using (var discord = _audioClients.GetValueOrDefault(channel.GuildId)?
                                                                .CreatePCMStream(AudioApplication.Music))
                        {

                            var modFunc = async (TimeSpan position) => await msg.ModifyAsync(a =>
                            {
                                a.Embed = banner.WithDescription(
                                    $"[{track.Title}]({track.Url})\n`{position:mm\\:ss} / {track.Duration:mm\\:ss}`").Build();
                            });

                            _positionFunc[channel.GuildId] = modFunc;
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
                                    await ProcessarBuffer(channel.GuildId, output, discord, token);
                                    await msg.ModifyAsync(a =>
                                    {
                                        a.Embed = banner.WithAuthor("Reproduzida").WithColor(Color.Default).Build();
                                        a.Components = new ComponentBuilder().Build();
                                    });
                                }
                                catch (OperationCanceledException)
                                {
                                    await msg.ModifyAsync(a =>
                                    {
                                        a.Embed = banner.WithAuthor("Interrompida").WithColor(Color.Default).Build();
                                        a.Components = new ComponentBuilder().Build();
                                    });
                                    _cts[channel.GuildId] = new CancellationTokenSource();
                                    token = _cts[channel.GuildId].Token;
                                }
                            }
                        }
                    }
                }

                _playerState[channel.GuildId] = PlaybackState.Stopped;
                _decorrido[channel.GuildId] = 0;
            }
            catch (OperationCanceledException)
            {
                _playerState[channel.GuildId] = PlaybackState.Stopped;
                _decorrido[channel.GuildId] = 0;
                await channel.SendEmbedText("Desconectado.");
                await SavePlaylistInfo(guild);
            }
            catch (Exception ex)
            {
                _playerState[channel.GuildId] = PlaybackState.Stopped;
                _decorrido[channel.GuildId] = 0;
                await channel.SendEmbedText($"Erro ao tocar musica: {ex}");
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
                await SavePlaylistInfo(guild);
            }

            await channel.SendEmbedText("Fila terminada");

            _tracks[channel.GuildId].Clear();
            _playerState[channel.GuildId] = PlaybackState.Stopped;
            await SavePlaylistInfo(guild);
            _decorrido[channel.GuildId] = 0;
            await Task.Delay(30000);

            if (token.IsCancellationRequested)
                if (_audioClients[channel.GuildId]?.ConnectionState == ConnectionState.Connected || _playerState[channel.GuildId] == PlaybackState.Stopped)
                    await _audioClients[channel.GuildId].StopAsync();
        }

        private async Task ResolveQuery(IGuildUser user, string query, ITextChannel channel)
        {
            if (!query.Trim().StartsWith("http"))
            {
                query = await Youtube.GetFirstVideoUrl(query);
            }

            Track[] tracks;
            if (query.Trim().StartsWith("https://open.spotify.com/"))
            {
                tracks = await GetSpotifyTracks(query);
            }
            else
            {
                tracks = new[] { await Youtube.GetTrackAsync(query) };
            }

            await EnqueueTracks(user, channel, tracks);
        }

        private EmbedBuilder PlayerBanner(Track track, IGuildUser queuedBy)
        {
            return new EmbedBuilder()
                .WithColor(Color.Red)
                .WithAuthor("Tocando Agora ♪")
                .WithDescription($"[{track.Title}]({track.Url})\n`00:00 / {track.Duration:mm\\:ss}`")
                .WithThumbnailUrl(track.ThumbUrl)
                .WithFooter($"Pedida por {queuedBy.DisplayName}", queuedBy.GetDisplayAvatarUrl());
        }

        private async Task<Track[]> GetSpotifyTracks(string query)
        {
            var match = query.Trim().Match(@"https\:\/\/open\.spotify\.com\/(?:intl-\w+/)?(playlist|track|album)\/([a-zA-Z0-9]+)");
            var type = match.Groups[1].Value;
            var id = match.Groups[2].Value;
            var tracks = await Spotify.GetListOfTracksAsync(id, type);
            return tracks;
        }

        private MessageComponent GetAudioControls(PlaybackState state)
        {
            var paused = state == PlaybackState.Paused;
            return new ComponentBuilder()
                .WithButton(null, "stop-music", ButtonStyle.Danger, emote["stop"], disabled: paused)
                .WithButton(null, "pause-music", paused ? ButtonStyle.Success : ButtonStyle.Secondary, paused ? emote["play"] : emote["pause"])
                .WithButton(null, "skip-music", ButtonStyle.Primary, emote["skip"], disabled: paused)
                .WithButton(null, "download-music", ButtonStyle.Success, emote["download"])
                .Build();
        }

        private async Task ProcessarBuffer(ulong guildId, Stream output, AudioOutStream discord, CancellationToken token)
        {
            float decorrido = 0;
            int sampleRate = 48000;
            int blockSize = sampleRate / 10;
            byte[] buffer = new byte[blockSize];
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }

                if (_playerState[guildId] == PlaybackState.Paused)
                    continue;

                var byteCount = await output.ReadAsync(buffer, 0, blockSize);

                decorrido += (float)byteCount / (2 * sampleRate);
                _decorrido[guildId] = decorrido / 2;

                if (byteCount <= 0)
                {
                    break;
                }

                try
                {
                    ProcessBufferVolume(ref buffer, blockSize, _volume[guildId]);
                    await discord!.WriteAsync(buffer, 0, byteCount);
                }
                catch (Exception e)
                {
                    _log.LogError(e, "Erro ao processar bloco de audio.");
                    await discord!.FlushAsync();
                }
            }
        }

        private void ProcessBufferVolume(ref byte[] buffer, int blockSize, int volume)
        {
            for (int i = 0; i < blockSize / 2; i++)
            {
                short sample = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                double gain = (volume / 100f);
                sample = (short)(sample * gain + 0.5);
                buffer[i * 2 + 1] = (byte)(sample >> 8);
                buffer[i * 2] = (byte)(sample & 0xff);
            }
        }

        public async Task UpdateControlsForMessage(SocketMessageComponent arg)
        {
            var components = GetAudioControls(_playerState[arg.GuildId ?? 0]);
            await arg.UpdateAsync(a => a.Components = components);
        }
    }
}
