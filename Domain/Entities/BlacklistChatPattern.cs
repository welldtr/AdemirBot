using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class BlacklistChatPattern : IEntity
    {
        [BsonId]
        public Guid PatternId { get; set; }
        public ulong GuildId { get; set; }
        public string Pattern { get; set; }
    }
}