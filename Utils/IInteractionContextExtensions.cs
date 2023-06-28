using Discord;
using Discord.WebSocket;

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

        public static ITextChannel GetTextChannel(this SocketMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            return channel;
        }

        public static async Task<IGuildUser> GetAuthorGuildUserAsync(this SocketMessage msg)
        {
            var channel = msg.GetTextChannel();
            var author = await channel.Guild.GetUserAsync(msg.Author?.Id ?? 0);
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
            var role = (me.Id == me.Id) ? "assistant" : "user";
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
    }
}
