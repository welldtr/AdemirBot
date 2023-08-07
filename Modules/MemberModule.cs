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
        [SlashCommand("avatar", "Mostra o Avatar de um usuario")]
        public async Task Avatar([Summary(description: "Usuario")] IUser usuario = null)
        {
            await DeferAsync();
            var usuarioGuilda = await Context.Guild.GetUserAsync((usuario ?? Context.User).Id);
            var url = (await Context.Guild.GetUserAsync(usuarioGuilda.Id)).GetDisplayAvatarUrl(size: 1024);

            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = " ";
                a.Embed = new EmbedBuilder()
                    .WithAuthor(usuarioGuilda)
                    .WithColor(Color.Default)
                    .WithCurrentTimestamp()
                    .WithImageUrl(url)
                    .Build();
            });
        }

        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        [SlashCommand("banner", "Mostra o Banner de um usuario")]
        public async Task Banner([Summary(description: "Usuario")] IUser usuario = null)
        {
            await DeferAsync();
            var usuarioGuilda = await Context.Guild.GetUserAsync((usuario ?? Context.User).Id);
            var restUser = await ((DiscordSocketClient)Context.Client).Rest.GetUserAsync(usuarioGuilda.Id);
            var url = restUser.GetBannerUrl();
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = " ";
                a.Embed = new EmbedBuilder()
                    .WithAuthor(usuarioGuilda)
                    .WithColor(Color.Default)
                    .WithCurrentTimestamp()
                    .WithImageUrl(url)
                    .Build();
            });
        }
    }
}
