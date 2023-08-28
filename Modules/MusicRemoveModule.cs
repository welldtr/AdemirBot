using Discord;
using Discord.Interactions;
using DiscordBot.Services;
using DiscordBot.Utils;

namespace DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.UseApplicationCommands)]
    [Group("remove", "Comandos de Remoção de músicas")]
    public class MusicRemoveModule : InteractionModuleBase
    {
        private readonly AudioService svc;

        public MusicRemoveModule(AudioService svc)
        {
            this.svc = svc;
        }

        [SlashCommand("index", "Remover uma musica na seguinte posição", runMode: RunMode.Async)]
        public async Task RemoveIndex([Summary("Posicao", description: "Posição da música a remover")] int index)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.RemoveIndex(user, channel, index);
            });
        }

        [SlashCommand("last", "Remover a ultima música da lista", runMode: RunMode.Async)]
        public async Task RemoveLast()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.RemoveLast(user, channel);
            });
        }

        [SlashCommand("member", "Remover uma musica na seguinte posição", runMode: RunMode.Async)]
        public async Task RemoveMemberTracks([Summary("Membro", description: "Membro ao remover músicas da playlist")] IUser member)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.RemoveMemberTracks(user, channel, member);
            });
        }

        [SlashCommand("range", "Remover uma musica na seguinte posição", runMode: RunMode.Async)]
        public async Task RemoveRange(
            [Summary("Inicial", description: "Posição inicial do intervalo a remover")] int n,
            [Summary("Final", description: "Posição final do intervalo a remover")] int m)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.RemoveRange(user, channel, n, m);
            });
        }
    }
}
