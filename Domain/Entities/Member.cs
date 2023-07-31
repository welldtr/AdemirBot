using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace DiscordBot.Domain.Entities
{
    public class Member : IEntity
    {
        [BsonId]
        public Guid Id { get; set; }
        public ulong MemberId { get; set; }
        public ulong GuildId { get; set; }
        public string MemberUserName { get; set; }
        public string MemberNickname { get; set; }
        public DateTime LastMessageTime { get; set; }
        public long XP { get; set; }
        public long MessageCount { get; set; }
        public long BumpCount { get; set; }
        public TimeSpan VoiceTime { get; set; }
        public TimeSpan MutedTime { get; set; }
        public TimeSpan StreamingTime { get; set; }
        public TimeSpan VideoTime { get; set; }
        public int Level { get; set; }
        public int LurkrLevel { get; set; }
        public long LurkrXP { get; set; }

        internal static Member FromGuildUser(IGuildUser user)
        {
            return new Member
            {
                Id = Guid.NewGuid(),
                GuildId = user.GuildId,
                MemberId = user.Id,
                MemberUserName = user.Username,
                MemberNickname = user.GlobalName
            };
        }
    }
}