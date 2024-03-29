﻿using Discord;
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
        [ComponentInteraction("stop-music", runMode: RunMode.Async)]
        public async Task StopMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.StopMusic(user, channel);
                await ((SocketSlashCommand)Context.Interaction)!.ModifyOriginalResponseAsync(a =>
                {
                    a.Components = null;
                });
            });
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("skip-music", runMode: RunMode.Async)]
        public async Task SkipMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.SkipMusic(user, channel);
                await ((SocketSlashCommand)Context.Interaction)!.ModifyOriginalResponseAsync(a =>
                {
                    a.Components = null;
                });
            });
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("pause-music", runMode: RunMode.Async)]
        public async Task PauseMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.PauseMusic(user, channel);
                await svc.UpdateControlsForMessage((SocketSlashCommand)Context.Interaction);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("back-music")]
        public async Task BackMusic()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.BackMusic(user, channel);
                await svc.UpdateControlsForMessage((SocketSlashCommand)Context.Interaction);
            });
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [ComponentInteraction("show-queue", runMode: RunMode.Async)]
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
            await svc.DownloadAtachment((SocketSlashCommand)Context.Interaction);
            await Context.Interaction.DeferAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("play", "Reproduz uma música, playlist ou álbum", runMode: RunMode.Async)]
        public async Task Play([Summary(description: "nome/link/track/playlist/album")] string busca)
        {
            _ = Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.PlayMusic(user, channel, busca);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("pause", "Pausa/Retoma a reprodução da música atual.", runMode: RunMode.Async)]
        public async Task Pause()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.PauseMusic(user, channel);
            });
            await svc.UpdateControlsForMessage((SocketSlashCommand)Context.Interaction);
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("volume", "Definir volume", runMode: RunMode.Async)]
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
        [SlashCommand("stop", "Interrompe a lista de reprodução.", runMode: RunMode.Async)]
        public async Task Stop()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.StopMusic(user, channel);
                await ((SocketSlashCommand)Context.Interaction)!.ModifyOriginalResponseAsync(a =>
                 {
                     a.Components = null;
                 });
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("back", "Pula para a música anterior da fila.", runMode: RunMode.Async)]
        public async Task Back([Summary(description: "Quantidade de faixas")] int qtd = 1)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.BackMusic(user, channel, qtd);
                await svc.UpdateControlsForMessage((SocketSlashCommand)Context.Interaction);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("skip", "Pula para a próxima música da fila.", runMode: RunMode.Async)]
        public async Task Skip([Summary(description: "Quantidade de faixas")] int qtd = 1)
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.SkipMusic(user, channel, qtd);
                await svc.UpdateControlsForMessage((SocketSlashCommand)Context.Interaction);
            });
        }


        [SlashCommand("download", "Baixa a música em execução", runMode: RunMode.Async)]
        public async Task Download()
        {
            await svc.DownloadAtachment((SocketSlashCommand)Context.Interaction);
            await RespondAsync();
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("replay", "Reinicia a música atual", runMode: RunMode.Async)]
        public async Task Replay()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ReplayMusic(user, channel);
                await svc.UpdateControlsForMessage((SocketSlashCommand)Context.Interaction);
            });
        }


        [RequireUserCanControlMusicPlayer]
        [SlashCommand("loop", "Habilita/Desabilita a repetição de faixa", runMode: RunMode.Async)]
        public async Task Loop()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ToggleLoopTrack(user, channel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("loopqueue", "Habilita/Desabilita a repetição de playlist", runMode: RunMode.Async)]
        public async Task LoopQueue()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ToggleLoopQueue(user, channel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("queue", "Lista as próximas 20 músicas da fila.", runMode: RunMode.Async)]
        public async Task Queue()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.ShowQueue(user, channel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("join", "Puxa o bot para o seu canal de voz", runMode: RunMode.Async)]
        public async Task Join()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.Join(user, channel, ((SocketGuildUser)Context.User).VoiceChannel);
            });
        }

        [RequireUserCanControlMusicPlayer]
        [SlashCommand("quit", "Remove o bot da chamada", runMode: RunMode.Async)]
        public async Task Quit()
        {
            await Context.EnsureUserCanUseThePlayer(async (user, channel) =>
            {
                await svc.QuitVoice(user, channel);
            });
        }
    }
}
