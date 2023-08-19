using Discord;
using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class GuildEvent : IEntity
    {
        [BsonId]
        public Guid GuildEventId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong GuildId { get; set; }
        public ulong EventId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Cover { get; set; }
        public DateTime ScheduledTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime LastAnnounceTime { get; set; }
        public string Location { get; set; }
        public GuildScheduledEventType Type { get; set; }
    }
}