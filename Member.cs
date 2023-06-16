using LiteDB;

namespace DiscordBot
{
    public class Membership
    {
        [BsonId]
        public Guid MembershipId { get; set; }
        public ulong MemberId { get; set; }
        public ulong GuildId { get; set; }
        public string MemberUserName { get; set; }
        public DateTime? DateJoined { get; set; }
        public DateTime? DateLeft { get; set; }
    }
}