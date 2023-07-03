using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;

namespace DiscordBot.Modules
{
    public class MusicModule : InteractionModuleBase
    {
        private readonly AudioService svc;

        public MusicModule(AudioService audioService)
        {
            svc = audioService;
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("volume", "Definir volume")]
        public async Task Volume(
            [Summary(description: "Volume (%)")] int volume)
        {
            await DeferAsync();

            if (volume > 0 && volume < 110)
            {
                await svc.SetVolume((ITextChannel)Context.Channel, volume);
                await ModifyOriginalResponseAsync(a => a.Content = $"Volume definido em {volume}%");
            }
            else
            {
                await ModifyOriginalResponseAsync(a => a.Content = "Volume inválido [0~110%]");
            }
        }

        [ComponentInteraction("stop-music")]
        public async Task StopMusic()
        {
            await svc.StopMusic((ITextChannel)Context.Channel);
            _ = Task.Run(async () => await ((SocketMessageComponent)Context.Interaction)!.UpdateAsync(a =>
            {
                a.Components = null;
            }));
            await Context.Interaction.DeferAsync();
        }

        [ComponentInteraction("skip-music")]
        public async Task SkipMusic()
        {
            await svc.SkipMusic((ITextChannel)Context.Channel);
            _ = Task.Run(async () => await ((SocketMessageComponent)Context.Interaction)!.UpdateAsync(a =>
            {
                a.Components = null;
            }));
            await Context.Interaction.DeferAsync();
        }

        [ComponentInteraction("pause-music")]
        public async Task PauseMusic()
        {
            await svc.PauseMusic((ITextChannel)Context.Channel);     
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            await Context.Interaction.DeferAsync();
        }

        [ComponentInteraction("download-music", false, RunMode.Async)]
        public async Task DownloadMusic()
        {
            await svc.DownloadAtachment((SocketMessageComponent)Context.Interaction);
            await Context.Interaction.DeferAsync();
        }
    }
}
