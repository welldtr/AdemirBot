using Discord;
using Discord.Audio;
using Discord.Interactions;
using DiscordBot.Domain;
using DiscordBot.Domain.Entities;
using System.Collections.Concurrent;

namespace DiscordBot.Modules
{
    public class MusicModule : InteractionModuleBase
    {
        private ConcurrentDictionary<ulong, IAudioClient> _audioClients;
        private ConcurrentDictionary<ulong, int> _volume;
        private ConcurrentDictionary<ulong, PlaybackState> _playerState;
        private ConcurrentDictionary<ulong, Track> _currentTrack;
        private ConcurrentDictionary<ulong, ConcurrentQueue<Track>> _tracks;
        private ConcurrentDictionary<ulong, CancellationTokenSource> _cts;

        private readonly Context db;
        public MusicModule(Context context)
        {
            db = context;
            _audioClients = new ConcurrentDictionary<ulong, IAudioClient>();
            _currentTrack = new ConcurrentDictionary<ulong, Track>();
            _volume = new ConcurrentDictionary<ulong, int>();
            _tracks = new ConcurrentDictionary<ulong, ConcurrentQueue<Track>>();
            _playerState = new ConcurrentDictionary<ulong, PlaybackState>();
            _cts = new ConcurrentDictionary<ulong, CancellationTokenSource>();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("volume", "Definir volume")]
        public async Task Volume(
            [Summary(description: "Volume (%)")] int volume)
        {
            await DeferAsync();

            if (volume > 0 && volume < 110)
            {
                var cfg = await db.ademirCfg.FindOneAsync(a => a.GuildId == Context.Guild.Id);

                if (cfg == null)
                {
                    cfg = new AdemirConfig
                    {
                        GuildId = Context.Guild.Id,
                        GlobalVolume = volume
                    };
                }
                else
                {
                    cfg.GlobalVolume = volume;
                }

                await db.ademirCfg.UpsertAsync(cfg);
                await ModifyOriginalResponseAsync(a => a.Content = $"Volume definido em {volume}% para a próxima execução.");
            }
            else
            {
                await ModifyOriginalResponseAsync(a => a.Content = "Volume inválido [0~110%]");
            }
        }
    }
}
