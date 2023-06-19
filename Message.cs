using LiteDB;

namespace DiscordBot
{
    public class Message
    {
        [BsonId]
        public ulong MessageId { get; set; }
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public DateTime MessageDate { get; set; }
        public long MessageLength { get; set; }
    }
}