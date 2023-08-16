using Discord;
using Discord.Audio;
using DiscordBot.Domain.Entities;
using DiscordBot.Services;

namespace DiscordBot.Domain.ValueObjects
{
    public class Playback
    {
        public Playback()
        {
            Tracks = new List<Track> { };
        }

        public float Decorrido { get; private set; }
        public PlaybackState PlayerState { get; private set; }
        public PlayMode PlayMode { get; private set; }
        public int Volume { get; private set; } = 100;
        public IAudioClient? AudioClient { get; private set; }
        public IAudioChannel? VoiceChannel { get; private set; }
        public int CurrentTrack { get; private set; }
        public CancellationTokenSource PlaybackCancellation { get; private set; } = new CancellationTokenSource();
        public List<Track> Tracks { get; private set; } = new List<Track>();

        public void LoadConfig(AdemirConfig ademirConfig)
        {
            Decorrido = ademirConfig.Position ?? 0;
            CurrentTrack = ademirConfig.CurrentTrack ?? 0;
            PlayerState = ademirConfig.PlaybackState!;
            PlayMode = ademirConfig.PlayMode;
            Volume = ademirConfig?.GlobalVolume ?? 100;
        }

        public void Clear()
        {
            CurrentTrack = 0;
            Tracks.Clear();
        }

        public void Reset()
        {
            Decorrido = 0;
            Clear();
            PlayerState = PlaybackState.Stopped;
        }

        internal void SetVolume(int volume)
        {
            Volume = volume;
        }

        internal void TogglePlayPause()
        {
            PlayerState = PlayerState == PlaybackState.Playing ? PlaybackState.Paused : PlaybackState.Playing;
        }

        internal async Task ConnectAsync(IVoiceChannel userVoiceChannel)
        {
            AudioClient = await userVoiceChannel.ConnectAsync(selfDeaf: true);
            VoiceChannel = userVoiceChannel;
        }

        internal void ToggleLoopTrack()
        {
            PlayMode = PlayMode == PlayMode.Normal ? PlayMode.LoopTrack : PlayMode.Normal;
        }

        internal void ToggleLoopQueue()
        {
            PlayMode = PlayMode == PlayMode.Normal ? PlayMode.LoopQueue : PlayMode.Normal;
        }

        internal void Stop()
        {
            Reset();
            InterruptPlayer();
        }

        internal async Task QuitAsync()
        {
            if (AudioClient != null)
            {
                await AudioClient!.StopAsync();
            }
            InterruptPlayer();
            Reset();

        }

        internal void Replay()
        {
            InterruptPlayer();
        }

        internal void Skip(int qtd)
        {
            CurrentTrack += qtd;
            InterruptPlayer();
        }

        internal void Back(int qtd)
        {
            CurrentTrack -= qtd;
            InterruptPlayer();
        }

        private void InterruptPlayer()
        {
            if (Interrupted != null)
            {
                Interrupted.Invoke(this, EventArgs.Empty);
            }
        }

        internal void SetCurrentTrack(int v)
        {
            CurrentTrack = v;
        }

        private async Task ProcessarBuffer(Stream output, AudioOutStream discord, CancellationToken token)
        {
            float decorrido = 0;
            int sampleRate = 48000;
            int blockSize = sampleRate / 10;
            byte[] buffer = new byte[blockSize];
            int fails = 0;
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }

                if (PlayerState == PlaybackState.Paused)
                    continue;

                var byteCount = await output.ReadAsync(buffer, 0, blockSize);

                decorrido += (float)byteCount / (2 * sampleRate);
                Decorrido = decorrido / 2;

                if (byteCount <= 0)
                {
                    break;
                }

                try
                {
                    ProcessBufferVolume(ref buffer, blockSize, Volume);
                    await discord!.WriteAsync(buffer, 0, byteCount);
                }
                catch (Exception e)
                {
                    fails++;
                    Console.WriteLine($"Erro ao processar bloco de audio. Falhas: {fails}: {e}");

                    if (fails <= 5)
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"Tentei {fails} vezes e falhei, desisto. {e}");
                        throw new BufferProcessingException();
                    }
                }
            }
        }

        private void ProcessBufferVolume(ref byte[] buffer, int blockSize, int volume)
        {
            for (int i = 0; i < blockSize / 2; i++)
            {
                short sample = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                double gain = (volume / 100f);
                sample = (short)(sample * gain + 0.5);
                buffer[i * 2 + 1] = (byte)(sample >> 8);
                buffer[i * 2] = (byte)(sample & 0xff);
            }
        }

        private event EventHandler Interrupted;
        internal async Task PlayAsync(Stream? output, AudioOutStream discord)
        {
            PlayerState = PlaybackState.Playing;
            await AudioClient!.SetSpeakingAsync(true);
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            EventHandler delegateInterrupt = (s, e) => { cts.Cancel(); };
            Interrupted += delegateInterrupt;

            try
            {
                await ProcessarBuffer(output, discord, token);
                switch (PlayMode)
                {
                    case PlayMode.Normal:
                        CurrentTrack++;
                        break;

                    case PlayMode.LoopQueue:
                        if (CurrentTrack == Tracks.Count)
                            CurrentTrack = 1;
                        else
                            CurrentTrack++;
                        break;
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                await discord.FlushAsync();
                Interrupted -= delegateInterrupt;
            }
        }
    }
}
