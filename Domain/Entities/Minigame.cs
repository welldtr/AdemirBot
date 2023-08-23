using DiscordBot.Domain.Enum;
using DiscordBot.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class MinigameMatch : IEntity
    {
        [BsonId]
        public Guid MinigameId { get; set; }
        public ulong GuildId { get; set; }

        [BsonRepresentation(BsonType.String)]
        public MinigameType MinigameType { get; set; }

        [BsonElement]
        public CharadeData Data { get; set; }
        public bool Finished { get; set; }
        public ulong Winner { get; set; }
        public DateTime StartDate { get; set; }
    }
}