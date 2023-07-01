using Discord.WebSocket;
using DiscordBot.Domain.Entities;

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
            await LogMessage(arg);
        }

        private async Task LogMessage(SocketMessage arg)
        {
            var channel = ((SocketTextChannel)arg.Channel);

            if (!arg.Author?.IsBot ?? false)
                await _db.messagelog.UpsertAsync(new Message
                {
                    MessageId = arg.Id,
                    ChannelId = channel.Id,
                    GuildId = channel.Guild.Id,
                    MessageDate = arg.Timestamp.UtcDateTime,
                    UserId = arg.Author?.Id ?? 0,
                    MessageLength = arg.Content.Length
                });
        }
    }
}
