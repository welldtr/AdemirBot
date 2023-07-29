using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Entities;
using DiscordBot.Modules.Modals;
using DiscordBot.Utils;
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
        public async Task ExcluirMacro([Summary(description: "Nome da macro")] IUser usuario = null)
        {
            await DeferAsync(ephemeral: true);
            var id = usuario?.Id ?? Context.User.Id;
            await ProcessCard(await Context.Guild.GetUserAsync(id));
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = "";
                a.Attachments = new[] { new FileAttachment("rankcard.png") };
            });
        }

        private async Task ProcessCard(IGuildUser user)
        {
            var member = await db.members.FindOneAsync(a => a.MemberId == user.Id);

            int width = 1600;
            int height = 400;
            System.Drawing.Color backgroundColor = ColorTranslator.FromHtml("#313338");

            Bitmap image = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(backgroundColor);

                // Adicionar retângulo de fundo
                System.Drawing.Color backgroundRectColor = ColorTranslator.FromHtml("#23272A");
                int backgroundRectSize = 300;
                int backgroundRectX = 50;
                int backgroundRectY = 50;
                int backgroundRectCornerRadius = 30;
                graphics.FillRoundedRectangle(new SolidBrush(backgroundRectColor), backgroundRectX, backgroundRectY, backgroundRectSize, backgroundRectSize, backgroundRectCornerRadius);

                // Adicionar retângulo adicional
                System.Drawing.Color additionalRectColor = ColorTranslator.FromHtml("#23272A");
                int additionalRectWidth = 1200;
                int additionalRectHeight = 60;
                int additionalRectX = 375;
                int additionalRectY = 290;
                int additionalRectCornerRadius = 30;
                graphics.FillRoundedRectangle(new SolidBrush(additionalRectColor), additionalRectX, additionalRectY, additionalRectWidth, additionalRectHeight, additionalRectCornerRadius);

                // Adicionar textos
                string text1 = user.GlobalName;
                string text2 = "RANK#?";
                string text3 = $"LEVEL {member.Level}";
                string text4 = $"{member.XP} XP";
                string text5 = "(? to Next Level)";

                Font font1 = new Font("gg sans SemiBold", 42);
                Font font2 = new Font("gg sans SemiBold", 42);
                Font font3 = new Font("gg sans SemiBold", 72);
                Font font4 = new Font("gg sans SemiBold", 40);
                Font font5 = new Font("gg sans SemiBold", 28);

                System.Drawing.Color textColor1 = ColorTranslator.FromHtml("#FFFFFF");
                System.Drawing.Color textColor2 = ColorTranslator.FromHtml("#99AAB5");
                System.Drawing.Color textColor3 = ColorTranslator.FromHtml("#FFFFFF");
                System.Drawing.Color textColor4 = ColorTranslator.FromHtml("#FFFFFF");
                System.Drawing.Color textColor5 = ColorTranslator.FromHtml("#99AAB5");

                int text1X = 372;
                int text1Y = 205;
                int text2X = 820;
                int text2Y = 70;
                int text3X = 1070;
                int text3Y = 34;
                int text4X = 923;
                int text4Y = 205;
                int text5X = 1248;
                int text5Y = 223;

                graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

                graphics.DrawString(text1, font1, new SolidBrush(textColor1), text1X, text1Y);
                graphics.DrawString(text2, font2, new SolidBrush(textColor2), text2X, text2Y);
                graphics.DrawString(text3, font3, new SolidBrush(textColor3), text3X, text3Y);
                graphics.DrawString(text4, font4, new SolidBrush(textColor4), text4X, text4Y);
                graphics.DrawString(text5, font5, new SolidBrush(textColor5), text5X, text5Y);

                var avatarUrl = user.GetGuildAvatarUrl(size: 512) ?? user.GetDisplayAvatarUrl(size: 512);

                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    using var client = new HttpClient();
                    var info = await client.GetStreamAsync(avatarUrl);
                    System.Drawing.Image avatar = System.Drawing.Image.FromStream(info);
                    Rectangle avatarRect = new Rectangle(75, 75, 250, 250);
                    graphics.DrawImage(avatar, avatarRect);
                }
            }

            // Salvar a imagem em um arquivo
            string filePath = "rankcard.png";
            image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
