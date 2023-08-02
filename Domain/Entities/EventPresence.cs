using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class EventPresence : IEntity
    {
        [BsonId]
        public Guid EventPresenceId { get; set; }
        public ulong MemberId { get; set; }
        public ulong GuildId { get; set; }
        public ulong EventId { get; set; }
        public TimeSpan ConnectedTime { get; set; }
    }
}