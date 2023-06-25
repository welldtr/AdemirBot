using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Utils;
using DiscordBot.Modules.Modals;

namespace DiscordBot.Modules
{
    public class BanModule : InteractionModuleBase
    {
        private readonly DiscordShardedClient _client;

        public BanModule(DiscordShardedClient client)
        {
            _client = client;
        }

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
                await _client.GetGuild(Context.Guild.Id).AddBanAsync(id);
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
                var user = _client.GetGuild(Context.Guild.Id).GetUser(id);
                if (user != null)
                    await user.KickAsync();
            }
            await Context.Channel.SendMessageAsync($"{memberIds.Length} Usuários Expulsos.");
        }
    }
}
