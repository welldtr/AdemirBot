using Microsoft.ML.Data;

namespace DiscordBot.Domain.ValueObjects
{
    public class DadosPredicao
    {
        [LoadColumn(0)]
        public float Data { get; set; }

        [LoadColumn(1)]
        public float QtdMembros { get; set; }
    }

    public class DadosPreditos
    {
        [ColumnName("Score")]
        public float DiaPredito { get; set; }
    }
}
