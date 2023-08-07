using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Domain.Enum;
using DiscordBot.Services;
using DiscordBot.Utils;
using MongoDB.Driver;
using SkiaSharp;

namespace DiscordBot.Modules
{
    public class MemberModule : InteractionModuleBase
    {
        private readonly Context db;

        public MemberModule(Context context)
        {
            db = context;
        }

        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        [SlashCommand("membercount", "Informa a quantidade de membros do server.")]
        public async Task MemberCount()
        {
            await DeferAsync();
            var progression = await db.progression.Find(t => t.Date == DateTime.Today).FirstOrDefaultAsync();

            if (progression == null)
            {
                await ModifyOriginalResponseAsync(a =>
                {
                    a.Embed = new EmbedBuilder()
                    .WithCurrentTimestamp()
                    .WithColor(Color.Default)
                    .WithFields(new[] { new EmbedFieldBuilder().WithName("Membros").WithValue($"{((SocketGuild)Context.Guild).MemberCount}") })
                    .Build();
                });
                return;
            }

            await ModifyOriginalResponseAsync(a =>
            {
                a.Embed = new EmbedBuilder()
                .WithCurrentTimestamp()
                .WithColor(Color.Default)
                .WithFields(new[] {
                    new EmbedFieldBuilder().WithName("Membros").WithValue($"{((SocketGuild)Context.Guild).MemberCount}"),
                    new EmbedFieldBuilder().WithName("Hoje").WithValue($"{progression.GrowthToday}")
                })
                .Build();
            });
        }

        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        [SlashCommand("avatar", "Mostra o Avatar de um usuário")]
        public async Task Avatar([Summary(description: "Usuario")] IUser usuario = null)
        {
            await DeferAsync(); 
            var url = usuario.GetAvatarUrl();

            using var client = new HttpClient();
            using (var ms = new MemoryStream())
            {
                var info = await client.GetStreamAsync(url);
                info.CopyTo(ms);
                ms.Position = 0;
                await ModifyOriginalResponseAsync(a => {
                    a.Content = " ";
                    a.Attachments = new[] { new FileAttachment(ms, "avatar.png") };
                });
            }
        }
    }
}
