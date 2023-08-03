using DiscordBot.Domain.Lurkr;
using DiscordBot.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class AdemirConfig : IEntity
    {
        [BsonId]
        public Guid AdemirConfigId { get; set; }
        public ulong GuildId { get; set; }
        public int? GlobalVolume { get; set; }
        public PlaybackState PlaybackState { get; set; }
        public PlayMode PlayMode { get; set; }
        public ulong? VoiceChannel { get; set; }
        [BsonRepresentation(BsonType.Double, AllowTruncation = true)]
        public float? Position { get; set; }
        public int? CurrentTrack { get; set; }
        public ulong AdemirRoleId { get; set; }
        public ulong AutoRoleId { get; set; }
        public ulong MinorRoleId { get; set; }
        public int AdemirConversationRPM { get; set; }
        public bool Premium { get; set; }
        public bool EnableRoleRewards { get; set; }
        public RoleReward[] RoleRewards { get; set; }
        public Dictionary<ulong, double> ChannelXpMultipliers { get; set; }
    }
}