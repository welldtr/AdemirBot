using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class ThreadChannel : IEntity
    {
        [BsonId]
        public ulong ThreadId { get; set; }
        public ulong MemberId { get; set; }
        public ulong GuildId { get; set; }
        public DateTime LastMessageTime { get; set; }
    }
}