using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class AdemirConfig : IEntity
    {
        [BsonId]
        public Guid AdemirConfigId { get; set; }
        public ulong GuildId { get; set; }
        public int? GlobalVolume { get; set; }
        public ulong AdemirRoleId { get; set; }
        public int AdemirConversationRPM { get; set; }
        public bool Premium { get; set; }
    }
}