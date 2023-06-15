using LiteDB;

namespace DiscordBot
{
    public class Bump
    {
        [BsonId]
        public Guid BumpId { get; set; }
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public DateTime BumpDate { get; set; }
        public long XP { get; set; }
    }
}