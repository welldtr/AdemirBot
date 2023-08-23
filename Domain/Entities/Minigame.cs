using Discord;
using Discord.WebSocket;
using DiscordBot.Domain.Enum;
using DiscordBot.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

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
        public MinigameData Data { get; set; }
        public bool Finished { get; set; }
        public ulong Winner { get; set; }

        internal CharadeData CharadeData()
        {
            return (CharadeData)Data;
        }
    }
}