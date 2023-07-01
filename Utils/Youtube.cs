using YoutubeExplode.Videos;
using YoutubeExplode;
using YoutubeSearchApi.Net.Services;
using YoutubeExplode.Exceptions;
using DiscordBot.Domain.Entities;

namespace DiscordBot.Utils
{
    public static class Youtube
    {
        public static async Task<Track> GetTrackAsync(string query)
        {
            var video = await new YoutubeClient().Videos.GetAsync(query);
            var track = new Track
            {
                Origin = "YouTube",
                Author = video.Author.ChannelTitle,
                Title = video.Title,
                AppendDate = DateTime.UtcNow,
                Duration = video.Duration ?? TimeSpan.Zero,
                VideoId = video.Id,
                Url = video.Url,
                ThumbUrl = video.Thumbnails.First().Url
            };
            return track;
        }

        public static async Task<string?> GetFirstVideoUrl(string query)
        {
            using (var httpClient = new HttpClient())
            {
                var client = new YoutubeSearchClient(httpClient);
                var responseObjetct = await client.SearchAsync(query);
                return responseObjetct.Results.FirstOrDefault()?.Url;
            }
        }
        public static async Task<string> ExtractAsync(this YoutubeClient _youtubeClient, Track track, CancellationToken cancellationToken)
        {
            try
            {
                var streamInfoSet = await _youtubeClient.Videos.Streams.GetManifestAsync(track.VideoId, cancellationToken: cancellationToken);
                var audioStreamInfo = streamInfoSet.GetAudioOnlyStreams().OrderByDescending(a => a.Bitrate).First();
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
                        var audioStreamInfo = streamInfoSet.GetAudioOnlyStreams().OrderByDescending(a => a.Bitrate).First();
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
