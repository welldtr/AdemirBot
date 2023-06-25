using Discord;
using Discord.Interactions;
using DiscordBot.Entities;
using MongoDB.Driver;

namespace DiscordBot.Modules
{
    public class MusicModule : InteractionModuleBase
    {
        private readonly Context db;
        public MusicModule(Context context)
        {
            db = context;
        }

        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("volume", "Definir volume")]
        public async Task Volume(
            [Summary(description: "Volume (%)")] int volume)
        {
            await DeferAsync();

            if (volume > 0 && volume < 110)
            {
                var cfg = await db.ademirCfg.GetByIdAsync(Context.Guild.Id);

                if (cfg == null)
                {
                    cfg = new AdemirConfig
                    {
                        GuildId = Context.Guild.Id,
                        GlobalVolume = volume
                    };
                }
                else
                {
                    cfg.GlobalVolume = volume;
                }

                await db.ademirCfg.UpsertAsync(cfg);
                await ModifyOriginalResponseAsync(a => a.Content = $"Volume definido em {volume}% para a próxima execução.");
            }
            else
            {
                await ModifyOriginalResponseAsync(a => a.Content = "Volume inválido [0~110%]");
            }
        }
    }
}
