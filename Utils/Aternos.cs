using HtmlAgilityPack;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System;
using System.Globalization;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace DiscordBot.Utils
{
    public class Aternos
    {
        private int timeout;
        private Dictionary<string, string> headers;
        private string TOKEN;
        private string SEC;
        private List<string> JavaSoftwares;
        private List<string> BedrockSoftwares;

        public Aternos(string usuario, string senha, string token, int timeout = 10)
        {
            using var md5 = MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(senha);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            var loginUrl = BuildURL("https://aternos.org/ajax/account/login", token);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", usuario),
                new KeyValuePair<string, string>("password", hash)
            });


            var client = new HttpClient(new CloudflareHttpHandler());
            client.SetHeader("Origin", "https://aternos.org");
            client.SetHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.SetHeader("Refer", "https://aternos.org/servers/");
            client.SetHeader("Sec-Ch-Ua", "\"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"114\", \"Microsoft Edge\";v=\"114\"");
            client.SetHeader("Sec-Ch-Ua-Platform", "\"Windows\"");
            client.SetHeader("Sec-Fetch-Dest", "empty");
            client.SetHeader("Sec-Fetch-Mode", "cors");
            client.SetHeader("Sec-Fetch-Site", "same-origin");
            client.SetHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36 Edg/114.0.1823.51");
            var pageresp = client.GetAsync("https://aternos.org/go/").GetAwaiter().GetResult();

            var cookies = pageresp.Headers.GetValues("Set-Cookie").FirstOrDefault();
            client.SetHeader("Cookie", cookies);

            client.SetHeader("Accept", "*/*");
            var stringContent = new StringContent(string.Empty);
            stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            var responseMsg = client.PostAsync("https://aternos.org/cdn-cgi/rum?", stringContent).GetAwaiter().GetResult();
            var cfray = responseMsg.Headers.GetValues("Cf-Ray").FirstOrDefault();
            client.SetHeader("Refer", "https://aternos.org/go/");

            client.SetHeader("X-Requested-With", "XMLHttpRequest");
            client.SetHeader("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            client.SetHeader("Accept-Encoding", "gzip, deflate, br");
            client.SetHeader("Cf-Ray", cfray);


            client.SetHeader("Cookie", cookies+ "; __cf_bm=1Q6sGSITEcWicczfkY4HpsKdCTQ2KVzJjCuF2F9gSrE-1687935731-0-Ab45SsqwD35SaM1loa3FFiqqqg0lhsmWc2mRDpAeOdx9J0ITmAaSGpeIhRF+N3g9mw==; _ga_70M94GH0FD=GS1.1.1687935728.1.0.1687935728.0.0.0; _ga=GA1.1.54203599.1687935728");

            formContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded; charset=UTF-8");
            responseMsg = client.PostAsync(loginUrl, formContent).GetAwaiter().GetResult();
            var resp = responseMsg.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var webdata = new HtmlDocument();
            this.timeout = timeout;
            this.headers = new Dictionary<string, string>();
            this.TOKEN = TOKEN;
            this.headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:72.0) Gecko/20100101 Firefox/72.0";
            this.headers["Cookie"] = cookies;
            this.SEC = GetSEC();
            this.JavaSoftwares = new List<string> { "Vanilla", "Spigot", "Forge", "Magma", "Snapshot", "Bukkit", "Paper", "Modpacks", "Glowstone" };
            this.BedrockSoftwares = new List<string> { "Bedrock", "Pocketmine-MP" };
        }
        static string encode(int nIn, int nBase)
        {
            var decimalPart = Convert.ToInt32(nIn.ToString(CultureInfo.InvariantCulture));
            int n = decimalPart / nBase;
            char c = "0123456789abcdefghijklmnopqrstuvwxyz"[decimalPart % nBase];
            return n > 0 ? encode(n, nBase) + c : c.ToString();
        }

        public string RandomString(int length)
        {
            Random random = new Random();
            var range = "abcdefghijklmnopqrstuvwxyz0123456789";
            var randomString = string.Join("", Enumerable.Range(0, 11).Select(s => range[random.Next() % 36])).PadRight(length, '0');

            return randomString;
        }


        public string GenerateAjaxToken(string url)
        {
            string key = RandomString(16);
            string value = RandomString(16);

            string cookie = "ATERNOS_SEC_" + key + "=" + value + ";path=" + url;
            // Use a biblioteca de manipulação de cookies do C# para definir o cookie

            return key + "%3A" + value;
        }

        public string BuildURL(string url, string token)
        {
            return url + "?" + $"SEC={GenerateAjaxToken(url)}&TOKEN={token}";
        }

        public string GetSEC()
        {
            string[] headers = this.headers["Cookie"].Split(";");
            foreach (string sec in headers)
            {
                if (sec.Substring(0, 12) == "ATERNOS_SEC_")
                {
                    string[] secParts = sec.Split("_");
                    if (secParts.Length == 3)
                    {
                        return string.Join(":", secParts[2].Split("="));
                    }
                }
            }
            return null;
        }

        public async Task<string> GetStatus()
        {
            string url = "https://aternos.org/server/";
            string webserver = await FilterCloudflare(url, headers);
            var webdata = new HtmlDocument();
            webdata.LoadHtml(webserver);
            var status = webdata.DocumentNode.SelectSingleNode("//span[@class='statuslabel-label']").InnerText;
            status = status.Trim();
            return status;
        }

        public async Task<string> StartServer()
        {
            string serverstatus = await GetStatus();
            if (serverstatus == "Online")
            {
                return "Server Already Running";
            }
            else
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters["headstart"] = "0";
                parameters["SEC"] = SEC;
                parameters["TOKEN"] = TOKEN;
                string startserver = await FilterCloudflare(url: "https://aternos.org/panel/ajax/start.php", parameters: parameters, headers: headers);

                // When pop up comes formatr confirmation:

                while (!(await GetStatus()).Contains("Preparing") && await GetStatus() != "Online")
                {
                    Thread.Sleep(10000);
                    startserver = await FilterCloudflare(url: "https://aternos.org/panel/ajax/confirm.php", parameters: parameters, headers: headers);
                }

                return "Server Started";
            }
        }

        public async Task<List<string>> GetPlayerInfo()
        {
            List<string> players = new List<string>();
            string webserver = await FilterCloudflare("https://aternos.org/players/", headers);
            var webdata = new HtmlDocument();
            webdata.LoadHtml(webserver);
            var status = webdata.DocumentNode.SelectNodes("//div[@class='playername']");
            foreach (var player in status)
            {
                players.Add(player.InnerText.Trim());
            }
            return players;
        }

        public async Task<string> StopServer()
        {
            string serverstatus = await GetStatus();
            if (serverstatus == "Offline")
            {
                return "Server Already Offline";
            }
            else
            {
                var parameters = new Dictionary<string, string>();
                parameters.Add("SEC", SEC);
                parameters.Add("TOKEN", TOKEN);
                string stopserver = await FilterCloudflare("https://aternos.org/panel/ajax/stop.php", parameters, headers);
                return "Server Stopped";
            }
        }

        public async Task<string> GetServerInfo()
        {
            string serverInfo = await FilterCloudflare("https://aternos.org/server/", headers);
            var serverData = new HtmlDocument();
            serverData.LoadHtml(serverInfo);
            var software = serverData.DocumentNode.SelectSingleNode("//span[@id='software']");

            if (software == null)
            {
                return null;
            }

            string softwareName = software.InnerText.Trim();

            if (ArrayContains(JavaSoftwares, softwareName))
            {
                string ip = serverData.DocumentNode.SelectSingleNode("//div[@class='server-ip mobile-full-width']").InnerText;
                ip = ip.Trim();
                string[] ipParts = ip.Split(" ");
                string serverIp = ipParts[0].Trim();
                string port = "25565(Optional)";
                return $"{serverIp},{port},{softwareName}";
            }
            else if (ArrayContains(BedrockSoftwares, softwareName))
            {
                string ip = serverData.DocumentNode.SelectSingleNode("//span[@id='ip']").InnerText;
                ip = ip.Trim();
                string port = serverData.DocumentNode.SelectSingleNode("//span[@id='port']").InnerText;
                port = port.Trim();
                return $"{ip},{port},{softwareName}";
            }

            return null;
        }

        public string GetParameters(Dictionary<string, string> parameters)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var p in parameters)
                query[p.Key] = p.Value;
            string queryString = query!.ToString();
            return queryString;
        }

        public async Task<string> FilterCloudflare(string url, Dictionary<string, string> parameters = null, Dictionary<string, string> headers = null)
        {
            var requests = new HttpClient(new CloudflareHttpHandler());

            if (headers != null)
                foreach (var h in headers)
                    requests.SetHeader(h.Key, h.Value);

            var gotData = await requests.GetStringAsync($"{url}?{GetParameters(parameters)}");
            int counter = 0;

            while (gotData.Contains("<title>Please Wait... | Cloudflare</title>") && counter < timeout)
            {
                requests = new HttpClient(new CloudflareHttpHandler());
                foreach (var h in headers)
                    requests.SetHeader(h.Key, h.Value);
                Thread.Sleep(1000);
                gotData = await requests.GetStringAsync($"{url}?{GetParameters(parameters)}");
                counter++;
            }

            if (gotData.Contains("<title>Please Wait... | Cloudflare</title>"))
            {
                Console.WriteLine("Cloudflare error!!");
                Environment.Exit(0);
            }

            return gotData;
        }

        public bool ArrayContains(Dictionary<string, string> array, string str)
        {
            foreach (string item in array.Keys)
            {
                if (str.ToLower().Contains(item.ToLower()) || item.ToLower().Contains(str.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }
        public bool ArrayContains(List<string> array, string str)
        {
            foreach (string item in array)
            {
                if (str.ToLower().Contains(item.ToLower()) || item.ToLower().Contains(str.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
