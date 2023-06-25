using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules.Modals
{
    public class MacroModal : IModal
    {
        public string Title => "Adicionar Macro";

        [InputLabel("Nome da Macro")]
        [ModalTextInput("nome", TextInputStyle.Short, placeholder: "Nome da macro usada (ex.: mensagem)")]
        public string Nome { get; set; }

        [InputLabel("Mensagem da Macro")]
        [ModalTextInput("mensagem", TextInputStyle.Paragraph, placeholder: "Mensagem que deve ser enviada ao digitar %nomedamacro")]
        public string Mensagem { get; set; }
    }
}
