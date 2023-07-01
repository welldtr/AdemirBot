using Discord;
using System.Diagnostics;

namespace DiscordBot.Utils
{
    public static class FFmpeg
    {
        public static Process? CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }

        public static async Task<FileAttachment> CreateMp3Attachment(string sourceFile, string attachmentFileName)
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{sourceFile}\" -ac 2  -f s16le -acodec libmp3lame -ar 44100 \"{sourceFile}.mp3\"",
                UseShellExecute = false
            });
            await p.WaitForExitAsync();
            return new FileAttachment($"{sourceFile}.mp3", attachmentFileName);
        }
    }
}
