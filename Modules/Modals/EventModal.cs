using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules.Modals
{
    public class EventModal : IModal
    {
        public string Title { get; set; } = "Criar Evento";

        [InputLabel("Nome do evento")]
        [ModalTextInput("nome", TextInputStyle.Short, placeholder: "Nome do Evento")]
        public string Nome { get; set; }

        [InputLabel("Data/Hora")]
        [ModalTextInput("data_hora",  TextInputStyle.Short, placeholder: "Data/Hora do Evento", maxLength: 16)]
        public string DataHora { get; set; }

        [InputLabel("Descrição do evento")]
        [ModalTextInput("descricao", TextInputStyle.Paragraph, placeholder: "Descrição do Evento")]
        public string Descricao { get; set; }
    }
}
