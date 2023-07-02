using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules.Modals
{
    public class EditMacroModal : IModal
    {
        public string Title { get; set; } = "Editar Macro";

        [InputLabel("Mensagem da Macro")]
        [ModalTextInput("mensagem", TextInputStyle.Paragraph, placeholder: "Mensagem que deve ser enviada ao digitar %nomedamacro")]
        public string Mensagem { get; set; }
    }
}
