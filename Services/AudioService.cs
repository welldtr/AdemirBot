using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using DiscordBot.Domain.Enum;
using DiscordBot.Domain.ValueObjects;
using DiscordBot.Utils;
using MongoDB.Bson;
using MongoDB.Driver;
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
        private readonly PaginationService paginator;

        public AudioService(Context context, DiscordShardedClient client, ILogger<AudioService> logger, PaginationService paginationService)
        {
            _db = context;
            _client = client;
            _log = logger;

            paginator = paginationService;
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
        }

        private async Task _client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState old, SocketVoiceState @new)
        {
            await Task.Delay(3000);
            if (user.Id == _client.CurrentUser.Id && @new.VoiceChannel == null)
            {
                var playback = old.VoiceChannel.GetPlayback();
                playback.Reset();
            }
        }

        private Task _client_MessageReceived(SocketMessage arg)
        {
            var guildId = ((SocketTextChannel)arg.Channel).Guild.Id;
            var guild = _client.Guilds.First(a => a.Id == guildId);
            var channel = ((ITextChannel)arg.Channel);
            var user = guild.GetUser(arg.Author?.Id ?? 0);

            if (user == null)
            {
                return Task.CompletedTask;
            }

            if (arg.Content == ">>help")
            {
                _ = Task.Run(async () => await Help(channel));
            }

            switch (arg.Content)
            {
                case ">>skip":
                    _ = Task.Run(async () => await SkipMusic(user, channel));
                    break;

                case string s when s.Matches(@"^>>skip (\d+)$"):
                    var skipstr = arg.Content.Match(@">>skip (\d+)").Groups[1].Value;
                    var qtd = int.Parse(skipstr);
                    _ = Task.Run(async () => await SkipMusic(user, channel, qtd));
                    break;

                case ">>stop":
                    _ = Task.Run(async () => await StopMusic(user, channel));
                    break;

                case ">>pause":
                    _ = Task.Run(async () => await PauseMusic(user, channel));
                    break;

                case ">>quit":
                    _ = Task.Run(async () => await QuitVoice(user, channel));
                    break;

                case ">>queue":
                    _ = Task.Run(async () => await ShowQueue(user, channel));
                    break;

                case ">>clear":
                    _ = Task.Run(async () => await ClearQueue(user, channel));
                    break;

                case ">>back":
                    _ = Task.Run(async () => await BackMusic(user, channel));
                    break;

                case string s when s.Matches(@"^>>back (\d+)$"):
                    var backstr = arg.Content.Match(@"^>>back (\d+)$").Groups[1].Value;
                    var back = int.Parse(backstr);
                    _ = Task.Run(async () => await BackMusic(user, channel, back));
                    break;

                case ">>replay":
                    _ = Task.Run(async () => await ReplayMusic(user, channel));
                    break;

                case ">>loopqueue":
                    _ = Task.Run(async () => await ToggleLoopQueue(user, channel));
                    break;

                case ">>loop":
                    _ = Task.Run(async () => await ToggleLoopTrack(user, channel));
                    break;

                case ">>shuffle":
                    _ = Task.Run(async () => await Shuffle(user, channel));
                    break;

                case ">>join":
                    var voicechannel = user.VoiceChannel;
                    _ = Task.Run(async () => await Join(user, channel, voicechannel));
                    break;

                case string s when s.Matches(@"^>>remove\s+(?:member\s+)<@(\d+)>$"):
                    var memberMention = arg.Content.Match(@"^>>remove\s+(?:member\s+)<@(\d+)>$").Groups[1].Value;
                    var idMember = ulong.Parse(memberMention);
                    var member = guild.GetUser(idMember);
                    _ = Task.Run(async () => await RemoveMemberTracks(user, channel, member));
                    break;

                case string s when s.Matches(@"^>>remove\s+(?:index\s+)\s+(\d+)$"):
                    var position = arg.Content.Match(@"^>>remove\s+(?:index\s+)\s+(\d+)$").Groups[1].Value;
                    var index = int.Parse(position);
                    _ = Task.Run(async () => await RemoveIndex(user, channel, index));
                    break;

                case string s when s.Matches(@"^>>remove\s+(?:range\s+)\s+(\d+)-(\d+)$"):
                    var matchnm = arg.Content.Match(@"^>>remove\s+(?:range\s+)\s+(\d+)-(\d+)$").Groups;
                    var n = int.Parse(matchnm[1].Value);
                    var m = int.Parse(matchnm[2].Value);
                    _ = Task.Run(async () => await RemoveRange(user, channel, n, m));
                    break;

                case ">>remove last":
                    _ = Task.Run(async () => await RemoveLast(user, channel));
                    break;

                case string s when s.Matches(@"^>>volume\s+(\d+)$"):
                    var volumestr = arg.Content.Match(@"^>>volume (\d+)$").Groups[1].Value;
                    var volume = int.Parse(volumestr);
                    _ = Task.Run(async () => await SetVolume(user, channel, volume));
                    break;

                case string s when s.Matches(@"^>>(.+)$"):
                    var query = arg.Content.Substring(2);
                    if (!string.IsNullOrEmpty(query))
                        _ = Task.Run(async () => await PlayMusic(user, channel, query));
                    break;
            }
            return Task.CompletedTask;
        }

        public async Task RemoveMemberTracks(IGuildUser user, ITextChannel channel, IUser member)
        {
            var playback = channel.GetPlayback();
            playback.Tracks.RemoveAll(a => a.UserId == member.Id);
            await channel.SendEmbedText($"Removidas todas as faixas do usuario **{member.Username}**");
        }

        public async Task RemoveIndex(IGuildUser user, ITextChannel channel, int index)
        {
            var playback = channel.GetPlayback();
            playback.Tracks.RemoveAt(index - 1);
            await channel.SendEmbedText($"Faixa numero **{index}** removida.");
        }

        public async Task RemoveLast(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            playback.Tracks.RemoveAt(playback.Tracks.Count - 1);
            await channel.SendEmbedText($"Ultima faixa da fila removida.");
        }

        public async Task RemoveRange(IGuildUser user, ITextChannel channel, int n, int m)
        {
            if (m < n)
            {
                await channel.SendEmbedText($"Intervalo de faixas {n}-{m} inválido.");
                return;
            }

            var playback = channel.GetPlayback();
            playback.Tracks.RemoveRange(n, m - n + 1);
            await channel.SendEmbedText($"Faixas de **{n}** a **{m}** removidas.");
        }

        public async Task Shuffle(IGuildUser user, ITextChannel channel)
        {
            await channel.SendEmbedText($"Modo aleatório ainda não disponível nessa versão");
        }

        public Task Join(IGuildUser user, ITextChannel channel, SocketVoiceChannel voicechannel)
        {
            if (voicechannel != null)
                _ = Task.Run(async () => await MoveToChannel(voicechannel));
            return Task.CompletedTask;
        }

        public async Task ToggleLoopQueue(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            playback.ToggleLoopQueue();
            await channel.SendEmbedText($"Playlist em modo {playback.PlayMode}");
        }

        public async Task ToggleLoopTrack(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            playback.ToggleLoopTrack();
            await channel.SendEmbedText($"Playlist em modo {playback.PlayMode}");
        }

        public async Task MoveToChannel(SocketVoiceChannel voicechannel)
        {
            var playback = voicechannel.GetPlayback();
            await playback.ConnectAsync(voicechannel);
        }

        private async Task _client_ShardReady(DiscordSocketClient client)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            foreach (var guild in client.Guilds)
            {
                try
                {
                    var ademirConfig = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);

                    var tracks = await _db.tracks.Find(a => a.GuildId == guild.Id)
                                        .SortBy(a => a.QueuePosition)
                                        .ToListAsync();

                    var track = tracks.FirstOrDefault();

                    PlaybackExtensions.InitPlayback(guild.Id);
                    var playback = guild.GetPlayback();
                    playback.LoadConfig(ademirConfig);
                    if (track != null)
                    {
                        var channel = guild.GetTextChannel(track.ChannelId);
                        var user = guild.GetUser(track.UserId);
                        var _ = Task.Run(() => PlayMusic(user, channel, tracks: tracks.ToArray()));
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

        public async Task ShowQueue(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            var plStr = new StringBuilder();
            int c = playback.CurrentTrack - 1;

            var currentPage = 1;
            var pagesize = 15M;
            var track = playback.Tracks[c];
            if (track != null)
            {
                currentPage = ((playback.Tracks.IndexOf(track) + 1) / (int)pagesize) + 1;
            }
            var numPaginas = (int)Math.Ceiling(playback.Tracks.Count / pagesize);
            var paginas = new List<Page>(Enumerable.Range(0, numPaginas).Select(a => new Page()).ToList());

            var tempoRestante = TimeSpan.FromSeconds((int)-playback.Decorrido + playback.Tracks.Where(a => a.QueuePosition > c).Sum(a => (int)a.Duration.TotalSeconds));

            for (var i = 0; i < numPaginas; i++)
            {
                var page = paginas[i];
                var lines = playback.Tracks
                    .Where(a => (int)Math.Ceiling((playback.Tracks.IndexOf(a) + 1) / pagesize) - 1 == i)
                    .Select(a => $"`[{(playback.Tracks.IndexOf(a) + 1).ToString().PadLeft(3)}]` `{(playback.Tracks.IndexOf(a) == c ? "▶" : " ")}{a.Title}` | `{a.Duration.FormatRushTime()}` (por: <@{a.UserId}>)");

                page.Description = string.Join("\n", lines) + $"\n\nTempo restante: {tempoRestante.FormatRushTime()}";
                page.Fields = new EmbedFieldBuilder[0];
            }

            if (playback.Tracks.Count == 0)
            {
                await channel.SendEmbedText("Esta é a ultima música da fila no momento.");
            }
            else
            {
                var message = new PaginatedMessage(paginas, $"Lista de Reprodução", Color.Default);
                message.CurrentPage = currentPage;
                await paginator.SendPaginatedMessageAsync(channel, message);
            }
        }

        private async Task SavePlaybackstate(SocketGuild guild)
        {
            var playback = guild.GetPlayback();
            if (playback == null)
                return;

            var ademirConfig = await _db.ademirCfg.FindOneAsync(a => a.GuildId == guild.Id);
            if (ademirConfig == null)
            {
                ademirConfig = new AdemirConfig
                {
                    GuildId = guild.Id
                };
            }

            ademirConfig.PlaybackState = playback.PlayerState;
            ademirConfig.PlayMode = playback.PlayMode;
            ademirConfig.Position = playback.Decorrido;
            ademirConfig.CurrentTrack = playback.CurrentTrack;
            ademirConfig.VoiceChannel = playback.VoiceChannel?.Id;
            ademirConfig.PlaybackState = playback.PlayerState;
            await _db.ademirCfg.UpsertAsync(ademirConfig);
        }

        private async Task SavePlaybackInfo(SocketGuild guild)
        {
            await SavePlaybackstate(guild);
            await SavePlaylistInfo(guild);
        }

        private async Task SavePlaylistInfo(SocketGuild guild)
        {
            var playback = guild.GetPlayback();
            var tracks = new List<Track>();

            await _db.tracks.DeleteAsync(a => a.GuildId == guild.Id);
            int position = 0;

            foreach (var track in playback.Tracks)
            {
                position++;
                track.QueuePosition = position;
                track._id = ObjectId.GenerateNewId();
            }

            if (playback.Tracks.Count > 0)
                await _db.tracks.AddAsync(playback.Tracks);
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
            var playback = channel.GetPlayback();
            if (tracks.Length > 1)
                await channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithFooter($"{tracks.Length} músicas adicionadas à fila por {user.DisplayName}", user.GetDisplayAvatarUrl())
                    .Build());

            else if (tracks.Length > 0 && playback.Tracks.Count > 0)
                await channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor("Adicionada à fila:")
                    .WithDescription($"[{tracks[0].Title}]({tracks[0].Url})\n`00:00 / {tracks[0].Duration.FormatRushTime()}`")
                    .WithFooter($"Adicionada por {user.DisplayName}", user.GetDisplayAvatarUrl())
                    .Build());

            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i] == null)
                {
                    await channel.SendEmbedText($"Não consegui informações da faixa nº {i}");
                    continue;
                }
                tracks[i].UserId = user.Id;
                tracks[i].AppendDate = DateTime.Now;
                tracks[i].GuildId = channel.GuildId;
                tracks[i].ChannelId = channel.Id;
                playback.Tracks.Add(tracks[i]);
            }
        }

        public async Task SetVolume(IGuildUser user, ITextChannel channel, int volume)
        {
            var playback = channel.GetPlayback();
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
                playback.SetVolume(volume);
            });
        }

        public Task PauseMusic(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            playback.TogglePlayPause();
            return Task.CompletedTask;
        }

        public async Task StopMusic(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            playback.Stop();
            var guild = _client.GetGuild(channel.GuildId);
            await SavePlaylistInfo(guild);
            await channel.SendEmbedText("Interrompido.");
        }

        private async Task ClearQueue(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            playback.Clear();
            await channel.SendEmbedText("Lista de reprodução limpa.");
            var guild = _client.GetGuild(channel.GuildId);
            await SavePlaylistInfo(guild);
        }

        public async Task Help(ITextChannel channel)
        {
            await channel.SendMessageAsync($" ", embed: new EmbedBuilder()
                .WithDescription(@"
### Comandos de Música (também com o prefixo >>)
- `/help`: Lista os comandos de áudio.
- `/play <link/track/playlist/album/artista>`: Reproduz uma música, playlist, artista ou álbum do Spotify ou Youtube.
- `/skip`: Pula para a próxima música da fila.
- `/back`: Pula para a música anterior da fila.
- `/replay`: Reinicia a música atual.
- `/pause`: Pausa/Retoma a reprodução da música atual.
- `/stop`: Interrompe completamente a reprodução de música.
- `/loop`: Habilita/Desabilita o modo de repetição de faixa.
- `/loopqueue`: Habilita/Desabilita o modo de repetição de playlist.
- `/queue`: Mostra a lista de reprodução.
- `/join`: Puxa o bot para o seu canal de voz.
- `/quit`: Remove o bot da chamada de voz.
- `/volume <valor>`: Ajusta o volume da música.
- `/remove member <membro>`: Remove as músicas de um membro da playlist.
- `/remove index <posicao>`: Remove uma musica da playlist na posição fornecida.
- `/remove range <inicio> <fim>`: Remove musicas de playlist no intervalor de inicio e fim.
- `/remove last`: Remove a última música da playlist.

Obs.: Os comandos acima só funcionam caso você esteja utilizando o player ou ninguém mais esteja.
").Build());
        }

        public async Task SkipMusic(IGuildUser user, ITextChannel channel, int qtd = 1)
        {
            var playback = channel.GetPlayback();
            var musicasRestantes = playback.Tracks.Count - playback.CurrentTrack;
            if (qtd <= musicasRestantes)
            {
                playback.Skip(qtd);
                var guild = _client.GetGuild(channel.GuildId);
                await SavePlaybackInfo(guild);
            }
            else
            {
                if (musicasRestantes == 0)
                    await channel.SendEmbedText($"Esta é a ultima música da fila.");
                else
                    await channel.SendEmbedText($"Existem apenas {musicasRestantes} musicas restantes");
            }
        }

        public async Task BackMusic(IGuildUser user, ITextChannel channel, int qtd = 1)
        {
            var playback = channel.GetPlayback();
            var musicasAnteriores = playback.CurrentTrack - 1;
            if (playback.CurrentTrack - qtd > 0)
            {
                playback.Back(qtd);
                var guild = _client.GetGuild(channel.GuildId);
                await SavePlaybackInfo(guild);
            }
            else
            {
                if (musicasAnteriores == 0)
                    await channel.SendEmbedText($"Esta é a primeira música da fila.");
                else
                    await channel.SendEmbedText($"Existem apenas {musicasAnteriores} musicas restantes");
            }
        }

        public Task ReplayMusic(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            playback.Replay();
            return Task.CompletedTask;
        }

        public async Task QuitVoice(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            await playback.QuitAsync();
            var guild = _client.GetGuild(channel.GuildId);
            await SavePlaylistInfo(guild);
            await channel.SendEmbedText("Desconectado.");
        }

        public async Task DownloadAtachment(SocketMessageComponent arg)
        {
            var playback = arg.GetPlayback();
            var youtubeClient = new YoutubeClient();
            var video = playback.Tracks[playback.CurrentTrack - 1];
            await arg.DeferLoadingAsync();
            var sourceFilename = await youtubeClient.ExtractAsync(video, CancellationToken.None);
            var fileName = video.Title.AsAlphanumeric() + ".mp3";
            var attachment = await FFmpeg.CreateMp3Attachment(sourceFilename, fileName);
            await arg.User.SendFileAsync(attachment);
            await arg.DeleteOriginalResponseAsync();
            File.Delete(sourceFilename);
            File.Delete(sourceFilename + ".mp3");
        }

        public async Task PlayMusic(IGuildUser user, ITextChannel channel, string query = null, params Track[] tracks)
        {
            IUserMessage msg = null;
            var playback = channel.GetPlayback();
            var guild = _client.GetGuild(channel.GuildId);
            string sourceFilename = string.Empty;

            try
            {
                var userVoiceChannel = user.VoiceChannel;

                if (userVoiceChannel == null)
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

                if (!resume && playback.PlayerState != PlaybackState.Stopped)
                {
                    return;
                }

                var components = GetAudioControls(PlaybackState.Playing);
                if (userVoiceChannel != null)
                {
                    if (!resume)
                    {
                        playback.SetCurrentTrack(1);
                    }

                    while (playback.CurrentTrack > 0 && playback.CurrentTrack <= playback.Tracks.Count)
                    {
                        var track = playback.Tracks[playback.CurrentTrack - 1];
                        await SavePlaylistInfo(guild);
                        var queuedBy = await channel.Guild.GetUserAsync(track.UserId);
                        var banner = PlayerBanner(track, queuedBy);

                        try
                        {
                            sourceFilename = await new YoutubeClient().ExtractAsync(track);
                            msg = await channel.SendMessageAsync(embed: banner.Build(), components: components);
                        }
                        catch (VideoUnplayableException ex)
                        {
                            await channel.SendEmbedText(
                                "Esta música não está disponível:",
                                $"{track.Title} - {track.Author} Duração: {track.Duration.FormatRushTime()}");
                            continue;
                        }

                        if (playback.AudioClient?.ConnectionState != ConnectionState.Connected)
                            await playback.ConnectAsync(userVoiceChannel);

                        var start = TimeSpan.Zero;
                        if (resume)
                        {
                            start = TimeSpan.FromSeconds(playback.Decorrido);
                            resume = false;
                        }

                        using (var ffmpeg = FFmpeg.CreateStream(sourceFilename, start))
                        using (var output = ffmpeg?.StandardOutput.BaseStream)
                        using (var discord = playback.AudioClient!.CreatePCMStream(AudioApplication.Mixed))
                        {
                            try
                            {
                                await playback.PlayAsync(output, discord);

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
                            }
                            catch (BufferProcessingException)
                            {
                                await msg.ModifyAsync(a =>
                                {
                                    a.Embed = banner.WithAuthor("Erro durante a reprodução").WithColor(Color.Red).Build();
                                    a.Components = new ComponentBuilder().Build();
                                });
                            }
                            await playback.AudioClient.SetSpeakingAsync(false);
                        }
                    }
                }

                playback.Reset();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Erro ao tocar música");
                await StopMusic(user, channel);
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

            playback.Reset();
            await SavePlaylistInfo(guild);

            if (playback.AudioClient?.ConnectionState == ConnectionState.Connected || playback.PlayerState == PlaybackState.Stopped)
                await playback.AudioClient!.StopAsync();
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
            else if (query.Trim().Matches(@"https\:\/\/www\.youtube\.com\/watch\?(?:v=[^&]*\&)?list=([^&]*)"))
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
                .WithDescription($"[{track.Title}]({track.Url})\n`00:00 / {track.Duration.FormatRushTime()}`")
                .WithThumbnailUrl(track.ThumbUrl)
                .WithFooter($"Pedida por {queuedBy.DisplayName}", queuedBy.GetDisplayAvatarUrl());
        }

        private async Task<Track[]> GetSpotifyTracks(string query)
        {
            var match = query.Trim().Match(@"https\:\/\/open\.spotify\.com\/(?:intl-\w+/)?(playlist|track|album|artist)\/([a-zA-Z0-9]+)");
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
                .WithButton(null, "back-music", ButtonStyle.Primary, PlayerEmote.Back, disabled: paused)
                .WithButton(null, "stop-music", ButtonStyle.Danger, PlayerEmote.Stop, disabled: paused)
                .WithButton(null, "pause-music", paused ? ButtonStyle.Success : ButtonStyle.Secondary, paused ? PlayerEmote.Play : PlayerEmote.Pause)
                .WithButton(null, "skip-music", ButtonStyle.Primary, PlayerEmote.Skip, disabled: paused)
                .WithButton(null, "show-queue", ButtonStyle.Secondary, PlayerEmote.Playlist)
                .WithButton(null, "download-music", ButtonStyle.Success, PlayerEmote.Download)
                .Build();
        }

        public async Task UpdateControlsForMessage(SocketMessageComponent arg)
        {
            var playback = arg.GetPlayback();
            var components = GetAudioControls(playback.PlayerState);
            await arg.UpdateAsync(a => a.Components = components);
        }
    }
}
