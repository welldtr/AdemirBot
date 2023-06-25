using YoutubeExplode.Videos;
using YoutubeExplode;
using YoutubeSearchApi.Net.Services;
using YoutubeExplode.Exceptions;

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
            try
            {
                var streamInfoSet = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id, cancellationToken: cancellationToken);
                var audioStreamInfo = streamInfoSet.GetAudioOnlyStreams().OrderByDescending(a => a.Bitrate).FirstOrDefault();
                var sourceFilename = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{audioStreamInfo.Container}");
                await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, sourceFilename, cancellationToken: cancellationToken);
                return sourceFilename;
            }
            catch (VideoUnplayableException)
            {
                await foreach (var result in _youtubeClient.Search.GetResultsAsync(video.Title))
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
