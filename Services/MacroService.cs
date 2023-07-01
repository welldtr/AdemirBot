using Discord;
using Discord.WebSocket;
using DiscordBot.Utils;

namespace DiscordBot.Services
{
    public class MacroService : Service
    {
        private Context _db;
        private DiscordShardedClient _client;

        public MacroService(Context context, DiscordShardedClient client)
        {
            _db = context;
            _client = client;
        }

        public override void Activate()
        {
            BindEventListeners();
        }

        public void BindEventListeners()
        {
            _client.MessageReceived += _client_MessageReceived;
        }

        private async Task _client_MessageReceived(SocketMessage arg)
        {
            await VerificarSeMacro(arg);
        }
        private async Task VerificarSeMacro(SocketMessage arg)
        {
            var channel = arg.GetTextChannel();
            var user = await arg.GetAuthorGuildUserAsync();
            if (user.GuildPermissions.Administrator && arg.Content.StartsWith("%") && arg.Content.Length > 1 && !arg.Content.Contains(' '))
            {
                var macro = await _db.macros
                    .FindOneAsync(a => a.GuildId == arg.GetGuildId() && a.Nome == arg.Content.Substring(1));

                if (macro != null)
                {
                    await channel.SendMessageAsync(macro.Mensagem, allowedMentions: AllowedMentions.None);
                    await channel.DeleteMessageAsync(arg);
                }
            }
        }
    }
}
