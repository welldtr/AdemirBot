using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class ServerNumberProgression : IEntity
    {
        [BsonId]
        public Guid ServerNumberProgressionId { get; set; }
        public ulong GuildId { get; set; }
        public long MemberCount { get; set; }
        public DateTime Date { get; set; }
    }
}