using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot
{
    public class Macro
    {
        [BsonId]
        public Guid MacroId { get; set; }
        public ulong GuildId { get; set; }
        public string Nome { get; set; }
        public string Mensagem { get; set; }

    }
}