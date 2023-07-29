using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Entities;
using DiscordBot.Modules.Modals;
using DiscordBot.Utils;
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
            await DeferAsync(ephemeral: true);
            var id = usuario?.Id ?? Context.User.Id;
            await ProcessCard(await Context.Guild.GetUserAsync(id));
            await RespondWithFileAsync(new FileAttachment("rankcard.png"));
        }

        private async Task ProcessCard(IGuildUser user)
        {
            var member = await db.members.FindOneAsync(a => a.MemberId == user.Id);

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

                // Adicionar retângulo adicional
                SKColor additionalRectColor = SKColor.Parse("#23272A");
                int additionalRectWidth = 1200;
                int additionalRectHeight = 60;
                int additionalRectX = 375;
                int additionalRectY = 290;
                int additionalRectCornerRadius = 30;
                canvas.DrawRoundRect(new SKRect(additionalRectX, additionalRectY, additionalRectX + additionalRectWidth, additionalRectY + additionalRectHeight), additionalRectCornerRadius, additionalRectCornerRadius, new SKPaint { Color = additionalRectColor });

                // Adicionar textos
                string textUsername = user.GlobalName;
                string textRank = "RANK#?";
                string textLevel = $"LEVEL {member.Level}";
                string textXp = $"{member.XP} XP";
                string textRemain = "(654 to Next Level)";

                var typeface = SKTypeface.FromFamilyName("gg sans", 600, 50, SKFontStyleSlant.Upright);
                SKFont fontUsername = new SKFont(typeface, 66);
                SKFont fontRank = new SKFont(typeface, 51);
                SKFont fontLvl = new SKFont(typeface, 96);
                SKFont fontXp = new SKFont(typeface, 47);
                SKFont fontRemain = new SKFont(typeface, 35);

                SKColor textColor1 = SKColor.Parse("#FFFFFF");
                SKColor textColor2 = SKColor.Parse("#99AAB5");
                SKColor textColor3 = SKColor.Parse("#FFFFFF");
                SKColor textColor4 = SKColor.Parse("#FFFFFF");
                SKColor textColor5 = SKColor.Parse("#99AAB5");

                (int x, int y) posUserName = (380, 268);
                (int x, int y) posRank = (1143, 104);
                (int x, int y) posLvl = (1570, 104);
                (int x, int y) posXp = (1232, 268);
                (int x, int y) posRemain = (1569, 268);

                canvas.DrawText(textUsername, posUserName.x, posUserName.y, fontUsername, new SKPaint { Color = textColor1 });
                canvas.DrawText(textRank, posRank.x, posRank.y, fontRank, new SKPaint { Color = textColor2, TextAlign = SKTextAlign.Right });
                canvas.DrawText(textLevel, posLvl.x, posLvl.y, fontLvl, new SKPaint { Color = textColor3, TextAlign = SKTextAlign.Right });
                canvas.DrawText(textXp, posXp.x, posXp.y, fontXp, new SKPaint { Color = textColor4, TextAlign = SKTextAlign.Right });
                canvas.DrawText(textRemain, posRemain.x, posRemain.y, fontRemain, new SKPaint { Color = textColor5, TextAlign = SKTextAlign.Right });

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

                // Salvar a imagem em um arquivo
                using (var image = surface.Snapshot())
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = File.OpenWrite("rankcard.png"))
                {
                    data.SaveTo(stream);
                }
            }
        }
    }
}
