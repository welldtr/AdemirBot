using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class UserRecommendation : IEntity
    {
        [BsonId]
        public Guid UserRecommendationId { get; set; }
        public ulong AuthorId { get; set; }
        public ulong UserRecommendedId { get; set; }
        public ulong GuildId { get; set; }
        public DateTime DateRecommendation { get; set; }
    }
}