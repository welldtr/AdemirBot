using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules.Modals
{
    public class MassBanModal : IModal
    {
        public string Title => "Banir Membros em Massa";

        [InputLabel("Membros")]
        [ModalTextInput("members", TextInputStyle.Paragraph, placeholder: "IDs dos membros a banir")]
        public string Membros { get; set; }
    }
}
