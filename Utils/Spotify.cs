using YoutubeExplode.Videos;
using YoutubeExplode;
using DiscordBot.Domain.Entities;
using SpotifyExplode;

namespace DiscordBot.Utils
{
    public static class Spotify
    {
        delegate ValueTask<List<SpotifyExplode.Tracks.Track>> SpotifyApi(string id);

        public static async Task<Track[]> GetListOfTracksAsync(string id, string type, CancellationToken token = default)
        {
            var spotify = new SpotifyClient();
            var apis = new Dictionary<string, SpotifyApi>()
            {
                { "track", async(i) => new List<SpotifyExplode.Tracks.Track> { await spotify.Tracks.GetAsync(i) } },
                { "playlist", async(i) => await spotify.Playlists.GetAllTracksAsync(i) },
                { "album", async(i) => await spotify.Albums.GetAllTracksAsync(i) }
            };

            var spotifyTracks = await apis[type](id);
            var playListTracks = new Track[spotifyTracks.Count];
            var downloads = Enumerable.Range(0, spotifyTracks.Count).Select(i => Task.Run(async () =>
            {
                try
                {
                    var youtubeId = await spotify.Tracks.GetYoutubeIdAsync(spotifyTracks[i].Url);
                    var video = await new YoutubeClient().Videos.GetAsync(VideoId.Parse(youtubeId!), token);
                    var track = await spotify.Tracks.GetAsync(spotifyTracks[i].Id);
                    playListTracks[i] = new Track
                    {
                        Origin = "Spotify",
                        Url = spotifyTracks[i].Url,
                        AppendDate = DateTime.UtcNow,
                        Duration = TimeSpan.FromMilliseconds(spotifyTracks[i].DurationMs),
                        TrackId = id,
                        VideoId = video.Url,
                        Title = spotifyTracks[i].Title,
                        Author = string.Join(", ", spotifyTracks[i].Artists.Select(a => a.Name)),
                        ThumbUrl = track.Album.Images.FirstOrDefault()?.Url,
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }));

            Task.WaitAll(downloads.ToArray());
            return playListTracks;
        }
    }
}
