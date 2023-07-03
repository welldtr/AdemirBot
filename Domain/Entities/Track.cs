using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class Track : IEntity
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong UserId { get; set; }
        public int QueuePosition { get; set; }
        public string VideoId { get; set; }
        public string TrackId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Origin { get; set; }
        public string Url { get; set; }
        public string ThumbUrl { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime AppendDate { get; set; }
    }
}