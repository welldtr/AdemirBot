using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class UserMention : IEntity
    {
        [BsonId]
        public Guid UserMentionId { get; set; }
        public ulong AuthorId { get; set; }
        public ulong MentionId { get; set; }
        public ulong GuildId { get; set; }
        public DateTime DateMentioned { get; set; }
    }
}