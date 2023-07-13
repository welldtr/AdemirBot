using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Domain.ValueObjects;
using DiscordBot.Utils;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Security.Cryptography;
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
        private ConcurrentDictionary<ulong, int> _currentTrack;
        private ConcurrentDictionary<ulong, PlayMode> _playmode;
        private ConcurrentDictionary<ulong, bool> _shuffle;
        private ConcurrentDictionary<ulong, List<Track>> _tracks;
        private ConcurrentDictionary<ulong, CancellationTokenSource> _cts;
        private ConcurrentDictionary<ulong, int> _volume;
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

        private void InitializeDictionaries()
        {
            _audioClients = new ConcurrentDictionary<ulong, IAudioClient>();
            _decorrido = new ConcurrentDictionary<ulong, float>();
            _currentTrack = new ConcurrentDictionary<ulong, int>();
            _tracks = new ConcurrentDictionary<ulong, List<Track>>();
            _playerState = new ConcurrentDictionary<ulong, PlaybackState>();
            _playmode = new ConcurrentDictionary<ulong, PlayMode>();
            _shuffle = new ConcurrentDictionary<ulong, bool>();
            _cts = new ConcurrentDictionary<ulong, CancellationTokenSource>();
            _volume = new ConcurrentDictionary<ulong, int>();
        }

        private async Task _client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState old, SocketVoiceState @new)
        {
            await Task.Delay(3000);
            if (user.Id == _client.CurrentUser.Id && @new.VoiceChannel == null)
            {
                _playerState[old.VoiceChannel.Guild.Id] = PlaybackState.Stopped;
                _tracks[old.VoiceChannel.Guild.Id].Clear();
            }
        }

        private async Task _client_MessageReceived(SocketMessage arg)
        {
            var guildId = ((SocketTextChannel)arg.Channel).Guild.Id;
            var guild = _client.Guilds.First(a => a.Id == guildId);
            var channel = ((ITextChannel)arg.Channel);
            var user = guild.GetUser(arg.Author?.Id ?? 0);

            if (user == null)
            {
                return;
            }

            switch (arg.Content)
            {
                case ">>skip":
                    _ = Task.Run(async () => await SkipMusic(channel));
                    break;

                case string s when s.Matches(@">>skip (\d+)"):
                    var skipstr = arg.Content.Match(@">>skip (\d+)").Groups[1].Value;
                    var qtd = int.Parse(skipstr);
                    _ = Task.Run(async () => await SkipMusic(channel, qtd));
                    break;

                case ">>transcript":
                    _ = Task.Run(async () => await StopMusic(channel));
                    break;

                case ">>stop":
                    _ = Task.Run(async () => await StopMusic(channel));
                    break;

                case ">>pause":
                    _ = Task.Run(async () => await PauseMusic(channel));
                    break;

                case ">>quit":
                    _ = Task.Run(async () => await QuitVoice(channel));
                    break;

                case ">>queue":
                    _ = Task.Run(async () => await ShowQueue(channel));
                    break;

                case ">>clear":
                    _ = Task.Run(async () => await ClearQueue(channel));
                    break;

                case ">>back":
                    _ = Task.Run(async () => await BackMusic(channel));
                    break;

                case string s when s.Matches(@">>back (\d+)"):
                    var backstr = arg.Content.Match(@">>back (\d+)").Groups[1].Value;
                    var back = int.Parse(backstr);
                    _ = Task.Run(async () => await BackMusic(channel, back));
                    break;

                case ">>replay":
                    _ = Task.Run(async () => await ReplayMusic(channel));
                    break;

                case ">>loopqueue":
                    _ = Task.Run(async () => await ToggleLoopQueue(channel));
                    break;

                case ">>loop":
                    _ = Task.Run(async () => await ToggleLoopTrack(channel));
                    break;

                case ">>shuffle":
                    _ = Task.Run(async () => await Shuffle(channel));
                    break;

                case ">>join":
                    var voicechannel = user.VoiceChannel;
                    if (voicechannel != null)
                        _ = Task.Run(async () => await MoveToChannel(voicechannel));
                    break;

                case string s when s.Matches(@">>volume (\d+)"):
                    var volumestr = arg.Content.Match(@">>volume (\d+)").Groups[1].Value;
                    var volume = int.Parse(volumestr);
                    _ = Task.Run(async () => await SetVolume(channel, volume));
                    break;

                case string s when s.StartsWith(">>"):
                    var query = arg.Content.Substring(2);
                    _ = Task.Run(async () => await PlayMusic(channel, user, query));
                    break;
            }
        }

        private async Task Shuffle(ITextChannel channel)
        {
            await channel.SendEmbedText($"Modo aleatório ainda não disponível nessa versão");
        }

        private async Task ToggleLoopQueue(ITextChannel channel)
        {
            _playmode[channel.GuildId] = _playmode[channel.GuildId] == PlayMode.Normal ? PlayMode.LoopQueue : PlayMode.Normal;
            await channel.SendEmbedText($"Playlist em modo {_playmode[channel.GuildId]}");
        }

        private async Task ToggleLoopTrack(ITextChannel channel)
        {
            _playmode[channel.GuildId] = _playmode[channel.GuildId] == PlayMode.Normal ? PlayMode.LoopTrack : PlayMode.Normal;
            await channel.SendEmbedText($"Playlist em modo {_playmode[channel.GuildId]}");
        }

        private async Task MoveToChannel(SocketVoiceChannel voicechannel)
        {
            _audioClients[voicechannel.Guild.Id] = await voicechannel.ConnectAsync(selfDeaf: true);
        }

        private async Task _client_ShardReady(DiscordSocketClient client)
        {
            emote = new Dictionary<string, Emote>()
            {
                {"clear", Emote.Parse("<:clear:1125482680490930196>") },
                {"stop", Emote.Parse("<:stop:1123770944784179210>") },
                {"play", Emote.Parse("<:play:1123770947984437259>") },
                {"pause", Emote.Parse("<:pause:1123770941235794033>") },
                {"skip", Emote.Parse("<:skip:1123771732243787887>") },
                {"repeat", Emote.Parse("<:repeat:1123770942863200377>") },
                {"shuffle", Emote.Parse("<:shuffle:1123770938425622591>") },
                {"back", Emote.Parse("<:back:1125481896416125040>") },
                {"playlist", Emote.Parse("<:playlist:1125481706783256707>") },
                {"download", Emote.Parse("<:download:1123771345667358720>") },
            };

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            foreach (var guild in client.Guilds)
            {
                try
                {
                    var ademirConfig = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
                    _tracks.TryAdd(guild.Id, new List<Track>());
                    _playerState[guild.Id] = PlaybackState.Stopped;
                    _cts[guild.Id] = new CancellationTokenSource();
                    _volume[guild.Id] = ademirConfig?.GlobalVolume ?? 100;
                    _decorrido[guild.Id] = 0;
                    _playmode[guild.Id] = PlayMode.Normal;
                    _currentTrack[guild.Id] = 0;

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
                            _currentTrack[guild.Id] = ademirConfig.CurrentTrack ?? 0;
                            _playerState[guild.Id] = ademirConfig.PlaybackState!;
                            _playmode[guild.Id] = ademirConfig.PlayMode;
                            var _ = Task.Run(() => PlayMusic(channel, user, tracks: tracks.ToArray()));
                        }
                    }
                }
                catch (Exception ex)
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

        private async Task ShowQueue(ITextChannel channel)
        {
            var plStr = new StringBuilder();
            int c = _currentTrack[channel.Guild.Id] - 1;
            for (int i = c; i < c + 15; i++)
            {
                if (i >= _tracks[channel.GuildId].Count)
                    break;
                var track = _tracks[channel.GuildId][i];
                var position = $"{(i == c ? "▶" : "#")}{i + 1}".PadLeft(3);
                var title = track.Title;
                var author = track.Author;
                var trackstring = $"`[{position}]`: `{title}` - `{author}` `({track.Duration:mm\\:ss})`";
                plStr.AppendLine(trackstring);
            }
            if (_tracks[channel.GuildId].Count == 0)
            {
                await channel.SendEmbedText("Esta é a ultima música da fila no momento.");
            }
            else
            {
                var tempoRestante = TimeSpan.FromSeconds((int)-_decorrido[channel.GuildId] + _tracks[channel.GuildId].Where(a => a.QueuePosition > c).Sum(a => (int)a.Duration.TotalSeconds));
                await channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor($"Lista de próximas 15 músicas:")
                    .WithDescription($"{plStr}\nTempo restante: {tempoRestante}")
                    .Build());
            }
        }

        private async Task SavePlaybackstate(SocketGuild guild)
        {
            var ademirConfig = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
            ademirConfig.PlaybackState = _playerState[guild.Id];
            ademirConfig.PlayMode = _playmode[guild.Id];
            ademirConfig.Position = _decorrido[guild.Id];
            ademirConfig.CurrentTrack = _currentTrack[guild.Id];
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
            var tracks = new List<Track>();
            for (int i = 0; i < 5; i++)
                if (_tracks.TryGetValue(guild.Id, out tracks))
                    break;

            await _db.tracks.DeleteAsync(a => a.GuildId == guild.Id);
            int position = 0;

            foreach (var track in tracks)
            {
                position++;
                track.QueuePosition = position;
                track._id = ObjectId.Empty;
            }
            await _db.tracks.AddAsync(tracks);
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
                _tracks[channel.GuildId].Add(track);
            }
        }

        public async Task SetVolume(ITextChannel channel, int volume)
        {
            var cfg = await _db.ademirCfg.FindOneAsync(a => a.GuildId == channel.GuildId);

            if (cfg == null)
            {
                cfg = new AdemirConfig
                {
                    GuildId = channel.GuildId,
                    GlobalVolume = volume
                };
            }
            else
            {
                cfg.GlobalVolume = volume;
            }

            await _db.ademirCfg.UpsertAsync(cfg);
            await channel.SendEmbedText($"Volume definido em {volume}%.");

            await Task.Run(() =>
            {
                for (int i = 0; i < 5; i++)
                    if (_volume.TryUpdate(channel.GuildId, volume, _volume[channel.GuildId]))
                        break;
            });
        }

        public Task PauseMusic(ITextChannel channel)
        {
            _playerState[channel.GuildId] = _playerState[channel.GuildId] == PlaybackState.Playing ? PlaybackState.Paused : PlaybackState.Playing;
            return Task.CompletedTask;
        }

        public async Task StopMusic(ITextChannel channel)
        {
            _currentTrack[channel.GuildId] = 0;
            _decorrido[channel.GuildId] = 0;
            _tracks[channel.GuildId].Clear();
            _playerState[channel.GuildId] = PlaybackState.Stopped;
            CancelStream(channel.GuildId);
            await channel.SendEmbedText("Interrompido.");
        }

        private async Task ClearQueue(ITextChannel channel)
        {
            _tracks[channel.GuildId].Clear();
            await channel.SendEmbedText("Lista de reprodução limpa.");
        }

        public async Task SkipMusic(ITextChannel channel, int qtd = 1)
        {
            var musicasRestantes = _tracks[channel.GuildId].Count - _currentTrack[channel.GuildId];
            if (qtd <= musicasRestantes)
            {
                _currentTrack[channel.GuildId] += qtd;
                CancelStream(channel.GuildId);
            }
            else
            {
                if (musicasRestantes == 0)
                    await channel.SendEmbedText($"Esta é a ultima música da fila.");
                else
                    await channel.SendEmbedText($"Existem apenas {musicasRestantes} musicas restantes");
            }
        }

        public async Task BackMusic(ITextChannel channel, int qtd = 1)
        {
            var musicasAnteriores = _currentTrack[channel.GuildId] - 1;
            if (_currentTrack[channel.GuildId] - qtd > 0)
            {
                _currentTrack[channel.GuildId] -= qtd;

                CancelStream(channel.GuildId);
            }
            else
            {
                if (musicasAnteriores == 0)
                    await channel.SendEmbedText($"Esta é a primeira música da fila.");
                else
                    await channel.SendEmbedText($"Existem apenas {musicasAnteriores} musicas restantes");
            }
        }

        public Task ReplayMusic(ITextChannel channel)
        {
            CancelStream(channel.GuildId);
            return Task.CompletedTask;
        }

        public async Task QuitVoice(ITextChannel channel)
        {
            await _audioClients[channel.GuildId].StopAsync();
            _playerState[channel.GuildId] = PlaybackState.Stopped;
            CancelStream(channel.GuildId);
            await channel.SendEmbedText("Desconectado.");
        }

        public async Task DownloadAtachment(SocketMessageComponent arg)
        {
            var youtubeClient = new YoutubeClient();
            var video = _tracks[arg.GuildId ?? 0][_currentTrack[arg.GuildId ?? 0] - 1];
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

            try
            {
                var voiceChannel = user.VoiceChannel;

                if (voiceChannel == null)
                {
                    await channel.SendEmbedText("Você precisa estar em um canal de voz.");
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
                var token = _cts[channel.GuildId].Token;
                if (voiceChannel != null && !token.IsCancellationRequested)
                {
                    if (!resume)
                    {
                        _currentTrack[channel.GuildId] = 1;
                    }

                    while (_currentTrack[channel.GuildId] <= _tracks[channel.GuildId].Count)
                    {
                        var track = _tracks[channel.GuildId][_currentTrack[channel.GuildId] - 1];
                        await SavePlaylistInfo(guild);
                        var queuedBy = await channel.Guild.GetUserAsync(track.UserId);
                        var banner = PlayerBanner(track, queuedBy);

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
                                switch (_playmode[channel.GuildId])
                                {
                                    case PlayMode.Normal:
                                        _currentTrack[channel.GuildId]++;
                                        break;

                                    case PlayMode.LoopQueue:
                                        if (_currentTrack[channel.GuildId] == _tracks[channel.GuildId].Count)
                                            _currentTrack[channel.GuildId] = 1;
                                        else
                                            _currentTrack[channel.GuildId]++;
                                        break;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                await msg.ModifyAsync(a =>
                                {
                                    a.Embed = banner.WithAuthor("Interrompida").WithColor(Color.Default).Build();
                                    a.Components = new ComponentBuilder().Build();
                                });
                                token = CancelStream(channel.GuildId);
                            }
                            catch (BufferProcessingException)
                            {
                                await msg.ModifyAsync(a =>
                                {
                                    a.Embed = banner.WithAuthor("Erro durante a reprodução").WithColor(Color.Red).Build();
                                    a.Components = new ComponentBuilder().Build();
                                });
                                token = CancelStream(channel.GuildId);
                            }
                        }
                    }
                }

                _playerState[channel.GuildId] = PlaybackState.Stopped;
                _currentTrack[channel.GuildId] = 0;
                _decorrido[channel.GuildId] = 0;
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
            await Task.Delay(10000);

            if (_audioClients[channel.GuildId]?.ConnectionState == ConnectionState.Connected || _playerState[channel.GuildId] == PlaybackState.Stopped)
                await _audioClients[channel.GuildId].StopAsync();
        }

        private CancellationToken CancelStream(ulong guildId)
        {
            _cts[guildId]?.Cancel();
            _cts[guildId] = new CancellationTokenSource();
            return _cts[guildId].Token;
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
            if (query.Trim().Matches(@"https\:\/\/www\.youtube\.com\/watch\?(?:v=[^&]*\&)?list=([^&]*)"))
            {
                tracks = await GetYoutubePlaylistTracks(query);
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

        private async Task<Track[]> GetYoutubePlaylistTracks(string query)
        {
            var match = query.Trim().Match(@"https\:\/\/www\.youtube\.com\/watch\?(?:v=[^&]*\&)?list=([^&]*)");
            var id = match.Groups[1].Value;
            var tracks = await Youtube.GetListOfTracksAsync(id);
            return tracks;
        }

        private MessageComponent GetAudioControls(PlaybackState state)
        {
            var paused = state == PlaybackState.Paused;
            return new ComponentBuilder()
                .WithButton(null, "back-music", ButtonStyle.Primary, emote["back"], disabled: paused)
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
            int fails = 0;
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
                    fails++;
                    _log.LogError(e, $"Erro ao processar bloco de audio. Falhas: {fails}");

                    if (fails <= 5)
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    else
                    {
                        _log.LogError(e, $"Tentei {fails} vezes e falhei, desisto.");
                        throw new BufferProcessingException();
                    }
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
