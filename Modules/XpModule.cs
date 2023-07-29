using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Entities;
using DiscordBot.Modules.Modals;
using DiscordBot.Utils;
using MongoDB.Driver;
using SkiaSharp;
using System.Drawing;
using System.Drawing.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Modules
{
    public class XpModule : InteractionModuleBase
    {
        private readonly Context db;

        public XpModule(Context context)
        {
            db = context;
        }

        [SlashCommand("rank", "Mostra o Card de Ranking no Server")]
        public async Task Rank([Summary(description: "Usuario")] IUser usuario = null)
        {
            await DeferAsync();
            var id = usuario?.Id ?? Context.User.Id;
            var cardfilename = await ProcessCard(Context.User as IGuildUser);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = " ";
                a.Attachments = new[] { new FileAttachment(cardfilename, "rank-card.png") };
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
                canvas.DrawRoundRect(new SKRect(backgroundRectX, backgroundRectY, backgroundRectX + backgroundRectSize, backgroundRectY + backgroundRectSize), backgroundRectCornerRadius, backgroundRectCornerRadius, new SKPaint { Color = backgroundRectColor });

                // Adicionar fundo da barra de progresso
                SKColor additionalRectColor = SKColor.Parse("#23272A");
                int additionalRectWidth = 1200;
                int additionalRectHeight = 60;
                int additionalRectX = 375;
                int additionalRectY = 290;
                int additionalRectCornerRadius = 30;
                canvas.DrawRoundRect(new SKRect(additionalRectX, additionalRectY, additionalRectX + additionalRectWidth, additionalRectY + additionalRectHeight), additionalRectCornerRadius, additionalRectCornerRadius, new SKPaint { Color = additionalRectColor });

                var typeface = SKTypeface.FromFamilyName("gg sans", 600, 50, SKFontStyleSlant.Upright);
                var levelMinXp = 50 * Math.Pow(member.Level - 2, 2) + 100;
                var levelMaxXp = 50 * Math.Pow(member.Level - 1, 2) + 100;
                var levelXp = member.XP - levelMinXp;
                var totalLevelXp = levelMaxXp - levelMinXp;
                var remainigXp = levelMaxXp - member.XP;
                var levelProgress = levelXp/totalLevelXp;

                // Adicionar preenchimento da barra de progresso
                SKColor additionalRect2Color = SKColor.Parse("#FFFFFFB0");
                int additionalRect2Width = Convert.ToInt32(1200 * levelProgress);
                int additionalRect2Height = 60;
                int additionalRect2X = 375;
                int additionalRect2Y = 290;
                int additionalRect2CornerRadius = 30;
                canvas.DrawRoundRect(new SKRect(additionalRect2X, additionalRect2Y, additionalRect2X + additionalRect2Width, additionalRect2Y + additionalRect2Height), additionalRect2CornerRadius, additionalRect2CornerRadius, new SKPaint { Color = additionalRect2Color });

                (string text, string color, int size, int x, int y) userName = (user.Username, "#FFFFFF", 66, 380, 268);
                (string text, string color, int size, int x, int y) rank = ($"RANK#{rankPosition}", "#99AAB5", 55, 1570, 154);
                (string text, string color, int size, int x, int y) lvl = ($"LEVEL {member.Level}", "#FFFFFF", 96, 1570, 104);
                (string text, string color, int size, int x, int y) xp = ($"{member.XP} XP", "#FFFFFF", 55, 1232, 268);
                (string text, string color, int size, int x, int y) remain = ($"({remainigXp} to Next Level)", "#99AAB5", 35, 1569, 268);

                canvas.DrawText(userName.text, userName.x, userName.y, new SKFont(typeface, userName.size), new SKPaint { Color = SKColor.Parse(userName.color) });
                canvas.DrawText(rank.text, rank.x, rank.y, new SKFont(typeface, rank.size), new SKPaint { Color = SKColor.Parse(rank.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(lvl.text, lvl.x, lvl.y, new SKFont(typeface, lvl.size), new SKPaint { Color = SKColor.Parse(lvl.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(xp.text, xp.x, xp.y, new SKFont(typeface, xp.size), new SKPaint { Color = SKColor.Parse(xp.color), TextAlign = SKTextAlign.Right });
                canvas.DrawText(remain.text, remain.x, remain.y, new SKFont(typeface, remain.size), new SKPaint { Color = SKColor.Parse(remain.color), TextAlign = SKTextAlign.Right });

                var avatarUrl = user.GetGuildAvatarUrl(size: 512) ?? user.GetDisplayAvatarUrl(size: 512);

                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    using var client = new HttpClient();
                    var ms = new MemoryStream();
                    var info = await client.GetStreamAsync(avatarUrl);
                    info.CopyTo(ms);
                    ms.Position = 0;
                    using var avatar = SKBitmap.Decode(ms);
                    var avatarRect = new SKRect(75, 75, 325, 325);
                    canvas.DrawBitmap(avatar, avatarRect);
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
