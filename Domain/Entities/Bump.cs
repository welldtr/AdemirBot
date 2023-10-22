using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class Bump : IEntity
    {
        [BsonId]
        public Guid BumpId { get; set; }
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public DateTime BumpDate { get; set; }
        public long XP { get; set; }
        public bool WelcomedByBumper { get; set; }
        public bool Rewarded { get; set; }
    }
}