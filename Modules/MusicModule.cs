using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Domain.Enum;
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

        [ComponentInteraction("back-music")]
        public async Task BackMusic()
        {
            await svc.BackMusic((ITextChannel)Context.Channel);
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            await Context.Interaction.DeferAsync();
        }

        [ComponentInteraction("download-music", false, RunMode.Async)]
        public async Task DownloadMusic()
        {
            await svc.DownloadAtachment((SocketMessageComponent)Context.Interaction);
            await Context.Interaction.DeferAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("play", "Reproduz uma música, playlist ou álbum")]
        public async Task Play([Summary(description: "nome/link/track/playlist/album")] string busca)
        {
            _ = Task.Run(async () => { 
                await svc.PlayMusic((ITextChannel)Context.Channel, (IGuildUser)Context.User, busca);
            });
            await RespondAsync();
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("pause", "Pausa/Retoma a reprodução da música atual.")]
        public async Task Pause()
        {
            await svc.PauseMusic((ITextChannel)Context.Channel);
            await RespondAsync();
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
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

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("stop", "Interrompe a lista de reprodução.")]
        public async Task Stop()
        {
            await svc.StopMusic((ITextChannel)Context.Channel);
            _ = Task.Run(async () => await ((SocketMessageComponent)Context.Interaction)!.UpdateAsync(a =>
            {
                a.Components = null;
            }));
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("back", "Pula para a música anterior da fila.")]
        public async Task Back([Summary(description: "Quantidade de faixas")] int qtd = 1)
        {
            await svc.BackMusic((ITextChannel)Context.Channel, qtd);
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("skip", "Pula para a próxima música da fila.")]
        public async Task Skip([Summary(description: "Quantidade de faixas")] int qtd = 1)
        {
            await svc.SkipMusic((ITextChannel)Context.Channel, qtd);
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("download", "Baixa a música em execução")]
        public async Task Download()
        {
            await svc.DownloadAtachment((SocketMessageComponent)Context.Interaction);
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("help", "Lista os comandos do módulo")]
        public async Task Help([Summary(description: "Módulo")] HelpType help)
        {
            switch (help)
            {
                case HelpType.Musica:
                    await svc.Help((ITextChannel)Context.Channel);
                    break;
            }
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("replay", "Reinicia a música atual")]
        public async Task Replay()
        {
            await svc.ReplayMusic((ITextChannel)Context.Channel);
            await svc.UpdateControlsForMessage((SocketMessageComponent)Context.Interaction);
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("loop", "Habilita/Desabilita a repetição de faixa")]
        public async Task Loop()
        {
            await svc.ToggleLoopTrack((ITextChannel)Context.Channel);
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("loopqueue", "Habilita/Desabilita a repetição de playlist")]
        public async Task LoopQueue()
        {
            await svc.ToggleLoopQueue((ITextChannel)Context.Channel);
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("queue", "Lista as próximas 20 músicas da fila.")]
        public async Task Queue()
        {
            await svc.ShowQueue((ITextChannel)Context.Channel);
            await RespondAsync();
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("join", "Puxa o bot para o seu canal de voz")]
        public async Task Join()
        {
            await RespondAsync();
            await svc.Join((ITextChannel)Context.Channel, ((SocketGuildUser)Context.User).VoiceChannel);
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("quit", "Remove o bot da chamada")]
        public async Task Quit()
        {
            await RespondAsync();
            await svc.QuitVoice((ITextChannel)Context.Channel);
        }
    }
}
