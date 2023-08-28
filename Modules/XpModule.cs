using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Entities;

namespace DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.Administrator)]
    [Group("xp", "Commandos de XP")]
    public class XpModule : InteractionModuleBase
    {
        private readonly Context db;

        public XpModule(Context context)
        {
            db = context;
        }

        [SlashCommand("add", "Adicionar XP a um usuário", runMode: RunMode.Async)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AddXP([Summary(description: "Usuario")] IUser usuario, [Summary(description: "XP a adicionar")] int xp)
        {
            await DeferAsync();
            var member = await db.members.FindOneAsync(a => a.GuildId == Context.Guild.Id && a.MemberId == usuario.Id);

            if (member == null)
            {
                member = Member.FromGuildUser(await Context.Guild.GetUserAsync(usuario.Id));
            }
            
            member.XP += xp;

            await db.members.UpsertAsync(member, a => a.GuildId == Context.Guild.Id && a.MemberId == usuario.Id);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = $"{xp}xp adicionado a {usuario.GlobalName}.";
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("remove", "Remover XP de um usuário", runMode: RunMode.Async)]
        public async Task RemoveXP([Summary(description: "Usuario")] IUser usuario, [Summary(description: "XP a remover")] int xp)
        {
            await DeferAsync();
            var member = await db.members.FindOneAsync(a => a.GuildId == Context.Guild.Id && a.MemberId == usuario.Id);

            if (member == null)
            {
                member = Member.FromGuildUser(await Context.Guild.GetUserAsync(usuario.Id));
            }

            member.XP -= xp;

            await db.members.UpsertAsync(member, a => a.GuildId == Context.Guild.Id && a.MemberId == usuario.Id);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = $"{xp}xp removido de {usuario.GlobalName}.";
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("set", "Definir o XP de um usuário", runMode: RunMode.Async)]
        public async Task SetXP([Summary(description: "Usuario")] IUser usuario, [Summary(description: "Novo XP")] int xp)
        {
            await DeferAsync();
            var member = await db.members.FindOneAsync(a => a.GuildId == Context.Guild.Id && a.MemberId == usuario.Id);

            if (member == null)
            {
                member = Member.FromGuildUser(await Context.Guild.GetUserAsync(usuario.Id));
            }

            member.XP = xp;

            await db.members.UpsertAsync(member, a => a.GuildId == Context.Guild.Id && a.MemberId == usuario.Id);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = $"Novo XP: {xp} atribuído a {usuario.GlobalName}.";
            });
        }
    }
}
