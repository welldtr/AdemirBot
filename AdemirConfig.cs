using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot
{
    public class AdemirConfig
    {
        [BsonId]
        public Guid AdemirConfigId { get; set; }
        public ulong GuildId { get; set; }
        public ulong AdemirRoleId { get; set; }
        public int AdemirConversationRPM { get; set; }
        public bool Premium { get; set; }
    }
}