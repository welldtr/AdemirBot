using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Enum;
using DiscordBot.Services;
using DiscordBot.Utils;
using MongoDB.Driver;
using SkiaSharp;

namespace DiscordBot.Modules
{
    public class XpModule : InteractionModuleBase
    {
        private readonly Context db;
        private readonly GuildPolicyService guildPolicy;
        private readonly PaginationService paginator;

        public XpModule(Context context, GuildPolicyService guildPolicy, PaginationService paginationService)
        {
            db = context;
            paginator = paginationService;
            this.guildPolicy = guildPolicy;
        }

        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        [SlashCommand("rank", "Mostra o Card de Ranking no Server")]
        public async Task Rank([Summary(description: "Usuario")] IUser usuario = null)
        {
            await DeferAsync();
            var id = usuario?.Id ?? Context.User.Id;
            var cardfilename = await ProcessCard(await Context.Guild.GetUserAsync(id));
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = " ";
                a.Attachments = new[] { new FileAttachment(cardfilename, "rank-card.png") };
            });
        }

        [SlashCommand("leaderboard", "Mostra o ranking atual dos membros")]
        public async Task Leaderboard()
        {
            await DeferAsync();
            var members = await db.members.Find(a => a.GuildId == Context.Guild.Id).SortByDescending(a => a.Level).ToListAsync();
            var member = members.FirstOrDefault(m => m.MemberId == Context.User.Id);

            var currentPage = 1;
            if (member != null)
            {
                currentPage = ((members.IndexOf(member) + 1)/10)+1;
            }
            var numPaginas = (int)Math.Ceiling(members.Count / 10M);
            var paginas = new List<Page>(Enumerable.Range(0, numPaginas).Select(a => new Page()).ToList());
            for (var i = 0; i < numPaginas; i++)
            {
                var page = paginas[i];
                var lines = members.Where(a => (int)Math.Ceiling((members.IndexOf(a) +1) / 10M)-1 == i).Select(a => $"**{members.IndexOf(a) + 1}.** <@{a.MemberId}>: **level {a.Level}** ({a.XP:n0}xp)");
                page.Description = string.Join("\n", lines);
                page.Fields = new EmbedFieldBuilder[0];
            }

            var message = new PaginatedMessage(paginas, $"Ranking {Context.Guild.Name}", new Color(0xb100c1), Context.User, new AppearanceOptions { });
            message.CurrentPage = currentPage;
            await paginator.SendPaginatedMessageAsync(Context.Channel, message);
        }


        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("togglerolerewards", "Ativar/Desativar o módulo de cargos por XP")]
        public async Task ToggleRoleRewards()
        {
            await DeferAsync();
            var config = await db.ademirCfg.FindOneAsync(a => a.GuildId == Context.Guild.Id);

            if (config == null)
            {
                config = new Domain.Entities.AdemirConfig
                {
                    GuildId = Context.Guild.Id,
                };
            }
            config.EnableRoleRewards = !config.EnableRoleRewards;

            await db.ademirCfg.UpsertAsync(config, a => a.GuildId == Context.Guild.Id);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = $"Cargos de recompensa por XP {(config.EnableRoleRewards ? "habilitados" : "desabilitados")}.";
            });
        }

        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        [SlashCommand("syncrolerewards", "Sincroniza os cargos do usuario pelo level")]
        public async Task SyncLevels([Summary(description: "Usuario")] IUser usuario = null)
        {
            var guild = Context.Guild;
            var admin = (await guild.GetUserAsync(Context.User.Id)).GuildPermissions.Administrator;

            await DeferAsync();

            var id = usuario?.Id ?? Context.User.Id;

            if (usuario?.Id != Context.User.Id && !admin)
            {
                await RespondAsync("Apenas administradores podem sincronizar outros usuários.", ephemeral: true);
                return;
            }

            var member = await db.members.FindOneAsync(a => a.MemberId == id && a.GuildId == Context.Guild.Id);

            if (member == null)
            {
                await ModifyOriginalResponseAsync(a =>
                {
                    a.Content = "Membro não tem informações no server.";
                });
            }
            else
            {
                var config = await db.ademirCfg.FindOneAsync(a => a.GuildId == member.GuildId);
                await guildPolicy.ProcessRoleRewards(config, member);
                await ModifyOriginalResponseAsync(a =>
                {
                    a.Content = $"Cargos sincronizados para o usuário {member.MemberUserName}.";
                });
            }
        }

        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        [SlashCommand("importlevelinfo", "Importar informações de level de outro bot")]
        public async Task ImportLevelInfo(ImportBot bot)
        {
            if (Context.User.Id != 596787570881462391 && Context.User.Id != 695465058913746964)
            {
                await RespondAsync("Você não pode usar esse comando. Fale com os embaixadores do projeto para solicitar liberação.");
                return;
            }

            await DeferAsync();

            switch (bot)
            {
                case ImportBot.Lurkr:
                    await Lurkr.ImportLevelInfo(Context.Client, Context.Guild, db);
                    break;
            }

            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = $"Informações de level importadas do bot {bot}";
            });
        }

        private async Task<string> ProcessCard(IGuildUser user)
        {
            var members = await db.members.Find(a => a.GuildId == user.GuildId).SortByDescending(a => a.Level).ToListAsync();
            var member = members.First(a => a.MemberId == user.Id);
            var rankPosition = members.IndexOf(member) + 1;
            int width = 1600;
            int height = 400;
            SKColor backgroundColor = SKColor.Parse("#313338");

            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                var canvas = surface.Canvas;
                canvas.Clear(backgroundColor);

                // Adicionar retângulo de fundo
                SKColor backgroundRectColor = SKColor.Parse("#23272A");
                int backgroundRectSize = 300;
                int backgroundRectX = 50;
                int backgroundRectY = 50;
                int backgroundRectCornerRadius = 25;
                canvas.DrawRoundRect(new SKRect(backgroundRectX, backgroundRectY, backgroundRectX + backgroundRectSize, backgroundRectY + backgroundRectSize), backgroundRectCornerRadius, backgroundRectCornerRadius, new SKPaint { IsAntialias = true, Color = backgroundRectColor });

                // Adicionar fundo da barra de progresso
                SKColor additionalRectColor = SKColor.Parse("#23272A");
                int additionalRectWidth = 1200;
                int additionalRectHeight = 60;
                int additionalRectX = 375;
                int additionalRectY = 290;
                int additionalRectCornerRadius = 30;
                canvas.DrawRoundRect(new SKRect(additionalRectX, additionalRectY, additionalRectX + additionalRectWidth, additionalRectY + additionalRectHeight), additionalRectCornerRadius, additionalRectCornerRadius, new SKPaint { IsAntialias = true, Color = additionalRectColor });

                var typeface = SKTypeface.FromFile("./shared/fonts/gg sans SemiBold.ttf");
                var levelMinXp = 50 * Math.Pow(member.Level - 1, 2) + 100;
                var levelMaxXp = 50 * Math.Pow(member.Level - 0, 2) + 100;
                var levelXp = member.XP - levelMinXp;
                var totalLevelXp = levelMaxXp - levelMinXp;
                var remainigXp = levelMaxXp - member.XP;
                var levelProgress = levelXp / totalLevelXp;

                // Adicionar preenchimento da barra de progresso
                SKColor additionalRect2Color = SKColor.Parse("#B0FFFFFF");
                int additionalRect2Width = Convert.ToInt32(1200 * levelProgress);
                int additionalRect2Height = 60;
                int additionalRect2X = 375;
                int additionalRect2Y = 290;
                int additionalRect2CornerRadius = 30;
                canvas.DrawRoundRect(new SKRect(additionalRect2X, additionalRect2Y, additionalRect2X + additionalRect2Width, additionalRect2Y + additionalRect2Height), additionalRect2CornerRadius, additionalRect2CornerRadius, new SKPaint { IsAntialias = true, Color = additionalRect2Color });

                (string text, string color, int size, int x, int y) userName = (user.Username, "#FFFFFF", 66, 380, 273);
                (string text, string color, int size, int x, int y) rank = ($"RANK#{rankPosition}", "#99AAB5", 55, 1570, 150);
                (string text, string color, int size, int x, int y) lvl = ($"LEVEL {member.Level}", "#FFFFFF", 96, 1570, 104);
                (string text, string color, int size, int x, int y) xp = ($"{member.XP:n0} XP", "#FFFFFF", 55, 1232, 273);
                (string text, string color, int size, int x, int y) remain = ($"({remainigXp} to Next Level)", "#99AAB5", 30, 1569, 273);
                (string text, string color, int size, int x, int y) voiceTime = ($"Audio: {member.VoiceTime.TotalHours:n0}h{member.VoiceTime.Minutes:00}", "#eeeeee", 28, 1570, 180);
                (string text, string color, int size, int x, int y) videoTime = ($"Video: {member.VideoTime.TotalHours:n0}h{member.VoiceTime.Minutes:00}", "#eeeeee", 28, 1570, 210);
                (string text, string color, int size, int x, int y) events = ($"Been to {member.EventsPresent:n0} events", "#eeeeee", 28, 1570, 240);

                canvas.DrawText(userName.text, userName.x, userName.y, new SKFont(typeface, userName.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(userName.color) });
                canvas.DrawText(rank.text, rank.x, rank.y, new SKFont(typeface, rank.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(rank.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(lvl.text, lvl.x, lvl.y, new SKFont(typeface, lvl.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(lvl.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(xp.text, xp.x, xp.y, new SKFont(typeface, xp.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(xp.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(remain.text, remain.x, remain.y, new SKFont(typeface, remain.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(remain.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(voiceTime.text, voiceTime.x, voiceTime.y, new SKFont(typeface, voiceTime.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(voiceTime.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(videoTime.text, videoTime.x, videoTime.y, new SKFont(typeface, videoTime.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(videoTime.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(events.text, events.x, events.y, new SKFont(typeface, events.size), new SKPaint { IsAntialias = true, Color = SKColor.Parse(events.color), TextAlign = SKTextAlign.Right });

                var avatarUrl = user.GetGuildAvatarUrl(size: 512) ?? user.GetDisplayAvatarUrl(size: 512);

                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    using var client = new HttpClient();
                    using (var ms = new MemoryStream())
                    {
                        var info = await client.GetStreamAsync(avatarUrl);
                        info.CopyTo(ms);
                        ms.Position = 0;
                        using var avatar = SKBitmap.Decode(ms);
                        var avatarRect = new SKRect(75, 75, 325, 325);
                        canvas.DrawBitmap(avatar, avatarRect);
                    }
                }

                var filename = Path.GetTempFileName();
                // Salvar a imagem em um arquivo
                using (var image = surface.Snapshot())
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = File.OpenWrite(filename))
                {
                    data.SaveTo(stream);
                }
                return filename;
            }
        }
    }
}
