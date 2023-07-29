using Discord.WebSocket;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

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
        public int Level { get; set; }
        public int LurkrLevel { get; set; }
        public long LurkrXP { get; set; }

        internal static Member FromSocketUser(SocketUser author)
        {
            return new Member
            {
                GuildId = author.Id,
                MemberId = author.Id,
                MemberUserName = author.Username,
                MemberNickname = author.GlobalName
            };
        }
    }
}