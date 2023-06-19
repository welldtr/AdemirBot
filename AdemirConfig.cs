﻿using LiteDB;

namespace DiscordBot
{
    public class AdemirConfig
    {
        [BsonId]
        public Guid AdemirConfigId { get; set; }
        public ulong GuildId { get; set; }
        public ulong AdemirRoleId { get; set; }
        public int AdemirConversationRPM { get; set; }
    }
}