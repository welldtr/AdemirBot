using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class BumpConfig : IEntity
    {
        [BsonId]
        public Guid BumpConfigId { get; set; }
        public ulong GuildId { get; set; }
        public ulong BumpChannelId { get; set; }
        public string? BumpMessageContent { get; set; }
        public ulong BumpBotId { get; set; }
        public long XPPerBump { get; set; }
    }
}