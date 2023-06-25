using System.Diagnostics;
using System.Globalization;

namespace DiscordBot.Utils
{
    public class FFmpeg
    {
        public static Process? CreateStream(string path, int volume)
        {
            var volPercent = (volume / 200M).ToString(CultureInfo.InvariantCulture);
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -af \"volume = {volPercent}\" -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }
    }
}
