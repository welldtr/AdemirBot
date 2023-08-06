using Discord;
using DiscordBot.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace DiscordBot.Utils
{

    public static class PlaybackExtensions
    {
        private static ConcurrentDictionary<ulong, Playback> _playback;
        static PlaybackExtensions()
        {
            _playback = new ConcurrentDictionary<ulong, Playback>();
        }
        public static void InitPlayback(ulong guildId)
        {
            _playback[guildId] = new Playback { };
        }

        public static Playback GetPlayback(ulong guildId)
        {
            bool ok = false;
            int retry = 0;
            Playback playback = null;
            while (!ok && retry < 10)
            {
                ok = _playback.TryGetValue(guildId, out playback);
                retry++;
            }
            return playback;
        }

        public static Playback GetPlayback(this IGuild guild)
        {
            return GetPlayback(guild.Id);
        }

        public static Playback GetPlayback(this ITextChannel channel)
        {
            return GetPlayback(channel.GuildId);
        }

        public static Playback GetPlayback(this IVoiceChannel channel)
        {
            return GetPlayback(channel.GuildId);
        }

        public static Playback GetPlayback(this IDiscordInteraction interaction)
        {
            return GetPlayback(interaction.GuildId ?? 0);
        }
    }
}
