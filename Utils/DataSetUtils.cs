using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Utils
{
    public static class DataSetUtils
    {
        public static string[] FemaleNames;

        public static async Task InitDatasets()
        {
            await DownloadWomanNamesDataset();
        }

        private static async Task DownloadWomanNamesDataset()
        {
            using (var httpClient = new HttpClient())
            {
                var femaleNames = await httpClient.GetStringAsync("https://www.cs.cmu.edu/Groups/AI/areas/nlp/corpora/names/female.txt");
                FemaleNames = femaleNames.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).OrderByDescending(a => a.Length).ToArray();
            }
        }
    }
}
