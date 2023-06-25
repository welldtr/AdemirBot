using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules.Modals
{
    public class MassKickModal : IModal
    {
        public string Title => "Expulsar Membros em Massa";

        [InputLabel("Membros")]
        [ModalTextInput("members", TextInputStyle.Paragraph, placeholder: "IDs dos membros a expulsar")]
        public string Membros { get; set; }
    }
}
