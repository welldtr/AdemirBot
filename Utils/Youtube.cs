using YoutubeExplode.Videos;
using YoutubeExplode;
using YoutubeSearchApi.Net.Services;
using SpotifyApi.NetCore.Authorization;
using SpotifyApi.NetCore;

namespace DiscordBot.Utils
{
    public static class Youtube
    {
        public static async Task<string> GetFirstVideoUrl(string query)
        {
            using (var httpClient = new HttpClient())
            {
                var client = new YoutubeSearchClient(httpClient);
                var responseObjetct = await client.SearchAsync(query);

                foreach (var video in responseObjetct.Results)
                {
                    return video.Url;
                }
                return null;
            }
        }
        public static async Task<string> ExtractAsync(this YoutubeClient _youtubeClient, Video video, CancellationToken cancellationToken)
        {
            var streamInfoSet = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id, cancellationToken: cancellationToken);
            var audioStreamInfo = streamInfoSet.GetAudioOnlyStreams().OrderByDescending(a => a.Bitrate).FirstOrDefault();
            var sourceFilename = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{audioStreamInfo.Container}");
            await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, sourceFilename, cancellationToken: cancellationToken);
            return sourceFilename;
        }
    }
}
