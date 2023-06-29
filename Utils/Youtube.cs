using YoutubeExplode.Videos;
using YoutubeExplode;
using YoutubeSearchApi.Net.Services;
using YoutubeExplode.Exceptions;
using DiscordBot.Domain.Entities;

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
        public static async Task<string> ExtractAsync(this YoutubeClient _youtubeClient, Track track, CancellationToken cancellationToken)
        {
            try
            {
                var streamInfoSet = await _youtubeClient.Videos.Streams.GetManifestAsync(track.VideoId, cancellationToken: cancellationToken);
                var audioStreamInfo = streamInfoSet.GetAudioOnlyStreams().OrderByDescending(a => a.Bitrate).FirstOrDefault();
                var sourceFilename = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{audioStreamInfo.Container}");
                await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, sourceFilename, cancellationToken: cancellationToken);
                return sourceFilename;
            }
            catch (VideoUnplayableException)
            {
                await foreach (var result in _youtubeClient.Search.GetResultsAsync(track.Title))
                {
                    try
                    {
                        var responseObjetct = await _youtubeClient.Videos.GetAsync(result.Url);
                        var streamInfoSet = await _youtubeClient.Videos.Streams.GetManifestAsync(responseObjetct.Id, cancellationToken: cancellationToken);
                        var audioStreamInfo = streamInfoSet.GetAudioOnlyStreams().OrderByDescending(a => a.Bitrate).FirstOrDefault();
                        var sourceFilename = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{audioStreamInfo.Container}");
                        await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, sourceFilename, cancellationToken: cancellationToken);
                        return sourceFilename;
                    }
                    catch (VideoUnplayableException)
                    {
                        continue;
                    }
                }
                throw;
            }
        }
    }
}
