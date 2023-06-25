using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Entities
{
    public class Macro : IEntity
    {
        [BsonId]
        public Guid MacroId { get; set; }
        public ulong GuildId { get; set; }
        public string Nome { get; set; }
        public string Mensagem { get; set; }

    }
}