using Discord;
using Discord.Interactions;
using DiscordBot.Utils;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot.Modules
{
    public class DevModule : InteractionModuleBase
    {
        public DevModule()
        {
        }

        [SlashCommand("md5", "Gera um hash MD5 de uma string")]
        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        public async Task GetMd5([Summary(description: "texto")] string text)
        {
            string hash = BitConverter.ToString(MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(text))).Replace("-", "");
            await RespondAsync(hash, ephemeral: true);
        }

        [SlashCommand("guid", "Gera um novo GUID")]
        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        public async Task GuidGuid()
        {
            await RespondAsync(Guid.NewGuid().ToString(), ephemeral: true);
        }

        [SlashCommand("memory", "Informação de sistema do ambiente do Ademir")]
        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        public async Task GetMemory()
        {
            var client = new MemoryMetricsClient();
            var metrics = client.GetMetrics();
            var info = $@"
Total: {metrics.Total}
Used : {metrics.Used}
Free : {metrics.Free}
";
            await RespondAsync(info, ephemeral: true);
        }
    }
}
