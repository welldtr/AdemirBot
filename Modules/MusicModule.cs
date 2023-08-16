using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Domain.Enum;
using DiscordBot.Services;
using DiscordBot.Utils;

namespace DiscordBot.Modules
{
    public class MusicModule : InteractionModuleBase
    {
        private readonly AudioService svc;

        public MusicModule(AudioService audioService)
        {
            svc = audioService;
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("stop-music")]
        public async Task StopMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.StopMusic(user, channel);
                await ((SocketMessageComponent)Context.Interaction)!.UpdateAsync(a =>
                {
                    a.Components = null;
                });
            });
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("skip-music")]
        public async Task SkipMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.SkipMusic(user, channel);
                await ((SocketMessageComponent)Context.Interaction)!.UpdateAsync(a =>
                {
                    a.Components = null;
                });
            });
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("pause-music")]
        public async Task PauseMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.PauseMusic(user, channel);
                await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("back-music")]
        public async Task BackMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.BackMusic(user, channel);
                await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            });
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("show-queue")]
        public async Task ShowQueue()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ShowQueue(user, channel);
            });
            await Context.Interaction.DeferAsync();
        }

        [ComponentInteraction("download-music", false, RunMode.Async)]
        public async Task DownloadMusic()
        {
            await svc.DownloadAtachment((SocketMessageComponent)Context.Interaction);
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("play", "Reproduz uma música, playlist ou álbum")]
        public async Task Play([Summary(description: "nome/link/track/playlist/album")] string busca)
        {
            _ = Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.PlayMusic(user, channel, busca);
            });
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("pause", "Pausa/Retoma a reprodução da música atual.")]
        public async Task Pause()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.PauseMusic(user, channel);
            });
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("volume", "Definir volume")]
        public async Task Volume(
            [Summary(description: "Volume (%)")] int volume)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                if (volume > 0 && volume < 110)
                {
                    await svc.SetVolume(user, channel, volume);
                }
                else
                {
                    await ModifyOriginalResponseAsync(a => a.Content = "Volume inválido [0~110%]");
                }
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("stop", "Interrompe a lista de reprodução.")]
        public async Task Stop()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.StopMusic(user, channel);
                await ((SocketMessageComponent)Context.Interaction)!.UpdateAsync(a =>
                 {
                     a.Components = null;
                 });
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("back", "Pula para a música anterior da fila.")]
        public async Task Back([Summary(description: "Quantidade de faixas")] int qtd = 1)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.BackMusic(user, channel, qtd);
                await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("skip", "Pula para a próxima música da fila.")]
        public async Task Skip([Summary(description: "Quantidade de faixas")] int qtd = 1)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.SkipMusic(user, channel, qtd);
                await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            });
        }


        [SlashCommand("download", "Baixa a música em execução")]
        public async Task Download()
        {
            await svc.DownloadAtachment((SocketMessageComponent)Context.Interaction);
            await RespondAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("replay", "Reinicia a música atual")]
        public async Task Replay()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ReplayMusic(user, channel);
                await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            });
        }


        [RequireUserCanControlMusicPlayer]
        [SlashCommand("loop", "Habilita/Desabilita a repetição de faixa")]
        public async Task Loop()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ToggleLoopTrack(user, channel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("loopqueue", "Habilita/Desabilita a repetição de playlist")]
        public async Task LoopQueue()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ToggleLoopQueue(user, channel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("queue", "Lista as próximas 20 músicas da fila.")]
        public async Task Queue()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ShowQueue(user, channel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("join", "Puxa o bot para o seu canal de voz")]
        public async Task Join()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.Join(user, channel, ((SocketGuildUser)Context.User).VoiceChannel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("quit", "Remove o bot da chamada")]
        public async Task Quit()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.QuitVoice(user, channel);
            });
        }
    }
}
