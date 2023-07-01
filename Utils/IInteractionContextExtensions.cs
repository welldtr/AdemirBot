using Discord;
using Discord.WebSocket;
using OpenAI.ObjectModels.RequestModels;

namespace DiscordBot.Utils
{
    public static class IInteractionContextExtensions
    {
        public static bool IsPremium(this IGuild guild)
        {
            var premiumGuilds = Environment.GetEnvironmentVariable("PremiumGuilds")!.Split(',');
            return premiumGuilds.Contains(guild.Id.ToString());
        }

        public static async Task<string> GetGPTAuthorNameAsync(this IMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            var author = await channel.GetUserAsync(msg?.Author?.Id ?? 0);
            var me = await channel.Guild.GetCurrentUserAsync();
            var name = msg!.Author.Id == me.Id ? "Ademir" : author.DisplayName;
            return name.AsAlphanumeric();
        }

        public static async Task<IUserMessage> SendEmbedText(this ITextChannel channel, string text)
        {
            return await channel.SendMessageAsync(embed: new EmbedBuilder()
                   .WithTitle(text)
                   .Build());
        }

        public static ITextChannel GetTextChannel(this SocketMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            return channel;
        }

        public static async Task<IGuildUser> GetAuthorGuildUserAsync(this SocketMessage msg)
        {
            var channel = msg.GetTextChannel();
            var author = await channel.Guild.GetUserAsync(msg.Author.Id);
            return author;
        }

        public static ulong GetGuildId(this SocketMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            return channel.GuildId;
        }

        public static async Task<string> GetGPTAuthorRoleAsync(this IMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            var me = await channel.Guild.GetCurrentUserAsync();
            var role = (msg.Author.Id == me.Id) ? "assistant" : "user";
            return role;
        }
        public static async Task<IMessage> GetReferenceAsync(this IMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            if (msg.Channel.Id != msg.Reference.ChannelId)
                channel = (ITextChannel) await channel.Guild.GetChannelAsync(msg.Reference.ChannelId);

            var msgRefer = await channel.GetMessageAsync(msg.Reference.MessageId.Value!);
            return msgRefer;
        }

        public static async Task GetRepliedMessages(this DiscordShardedClient _client, ITextChannel channel, IMessage message, List<ChatMessage> msgs)
        {
            var guild = _client.GetGuild(channel.GuildId);
            while (message.Reference != null && message.Reference.MessageId.IsSpecified)
            {
                if (channel.Id != message.Reference.ChannelId)
                    channel = (ITextChannel)guild.GetChannel(message.Reference.ChannelId);

                message = await message.GetReferenceAsync();
                var autor = await message.GetGPTAuthorRoleAsync();
                var nome = await message.GetGPTAuthorNameAsync();
                if (message.Type == MessageType.Default)
                    msgs.Insert(0, new ChatMessage(autor, message.Content.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
            }
        }

        public static async Task GetThreadMessages(this DiscordShardedClient _client, IThreadChannel thread, IMessage message, List<ChatMessage> msgs)
        {
            var guild = _client.GetGuild(thread.GuildId);

            var msgsThread = await thread.GetMessagesAsync(message.Id, Direction.Before).FlattenAsync();
            foreach (var m in msgsThread)
            {
                var autor = await m.GetGPTAuthorRoleAsync();
                var nome = await m.GetGPTAuthorNameAsync();
                if (m.Type == MessageType.Default)
                    msgs.Insert(0, new ChatMessage(autor, m.Content.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
            }

            var firstMsg = msgsThread.LastOrDefault();
            var ch = (ITextChannel)guild.GetChannel(firstMsg!.Reference.ChannelId);
            await _client.GetRepliedMessages(ch, firstMsg, msgs);
        }
    }
}
