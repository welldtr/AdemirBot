using Discord;
using Discord.Interactions;
using DiscordBot.Utils;
using DiscordBot.Modules.Modals;

namespace DiscordBot.Modules
{
    public class BanModule : InteractionModuleBase
    {
        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("massban", "Banir membros em massa.")]
        public async Task MassBan()
            => await Context.Interaction.RespondWithModalAsync<MassBanModal>("mass_ban");

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("masskick", "Expulsar membros em massa.")]
        public async Task MassKick()
            => await Context.Interaction.RespondWithModalAsync<MassKickModal>("mass_kick");

        [ModalInteraction("mass_ban")]
        public async Task BanResponse(MassBanModal modal)
        {
            var memberIds = StringUtils.SplitAndParseMemberIds(modal.Membros);
            await DeferAsync();
            foreach (var id in memberIds)
            {
                await (await Context.Client.GetGuildAsync(Context.Guild.Id)).AddBanAsync(id);
            }
            await Context.Channel.SendMessageAsync($"{memberIds.Length} Usuários Banidos.");
        }

        [ModalInteraction("mass_kick")]
        public async Task KickResponse(MassKickModal modal)
        {
            var memberIds = StringUtils.SplitAndParseMemberIds(modal.Membros);
            await DeferAsync();
            foreach (var id in memberIds)
            {
                var user = await (await Context.Client.GetGuildAsync(Context.Guild.Id)).GetUserAsync(id);
                if (user != null)
                    await user.KickAsync();
            }
            await Context.Channel.SendMessageAsync($"{memberIds.Length} Usuários Expulsos.");
        }
    }
}
