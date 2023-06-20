using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot
{
    public class DenunciaConfig
    {
        [BsonId]
        public Guid DenunciaId { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
    }
}