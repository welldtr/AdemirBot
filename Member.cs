using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot
{
    public class Member
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