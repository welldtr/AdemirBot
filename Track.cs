using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot
{
    public class Track
    {
        [BsonId]
        public ulong TrackID { get; set; }
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public string Url { get; set; }
        public DateTime AppendDate { get; set; }
    }
}