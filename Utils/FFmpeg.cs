using Discord;
using System.Diagnostics;

namespace DiscordBot.Utils
{
    public static class FFmpeg
    {
        public static Process? CreateStream(string path, TimeSpan start = default)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -ss {start} -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
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

        public static string ConvertOggToMp3(string oggFilePath)
        {
            string mp3FilePath = GetTempFilePath(".mp3");

            // Comando para executar o ffmpeg e converter o arquivo OGG para MP3
            string ffmpegCommand = $"-i \"{oggFilePath}\" \"{mp3FilePath}\"";

            // Executa o comando do ffmpeg
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegCommand,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();

                // Aguarda a conclusão do processo
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    return mp3FilePath;
                }
                else
                {
                    throw new Exception("Erro ao converter o arquivo OGG para MP3.");
                }
            }
        }

        private static string GetTempFilePath(string extension)
        {
            string tempFileName = Guid.NewGuid().ToString() + extension;
            string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), tempFileName);
            return tempFilePath;
        }
    }
}
