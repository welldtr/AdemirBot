using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class Member : IEntity
    {
        [BsonId]
        public ulong MemberId { get; set; }
        public ulong GuildId { get; set; }
        public string MemberUserName { get; set; }
        public string MemberNickname { get; set; }
        public DateTime LastMessageTime { get; set; }
        public long XP { get; set; }
        public long MessageCount { get; set; }
        public int level { get; set; }
    }
}