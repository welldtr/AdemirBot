using MongoDB.Bson.Serialization.Attributes;

namespace DiscordBot.Domain.Entities
{
    public class Denuncia : IEntity
    {
        [BsonId]
        public Guid DenunciaId { get; set; }
        public ulong GuildId { get; set; }
        public ulong ReportId { get; set; }
        public ulong DenunciadoUserId { get; set; }
        public ulong DenuncianteUserId { get; set; }
        public ulong? TestemunhaUserId { get; set; }
        public DateTime DenunciaDate { get; set; }
        public string? PrintUrl { get; set; }
        public bool? Deferida { get; set; }
        public string Conteudo { get; set; }
    }
}