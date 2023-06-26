using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class DenunciaConfig : IEntity
    {
        [BsonId]
        public Guid DenunciaId { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
    }
}