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

        [RequireUserPermission(GuildPermission.UseApplicationCommands)]
        [SlashCommand("membergraph", "Mostra a evolução de membros")]
        public async Task Membergraph([Summary(description: "Dias")] int dias = 7)
        {
            await DeferAsync();

            var chartFileName = await ProcessMemberGraph(Context.Guild, dias);
            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = " ";
                a.Attachments = new[] { new FileAttachment(chartFileName, "graph.png") };
            });
        }

        private async Task<string> ProcessMemberGraph(IGuild guild, int dias)
        {
            var progression = await db.progression.Find(a => a.GuildId == 917286921259089930 && a.Date > DateTime.UtcNow.Date.AddDays(-dias)).SortBy(a => a.Date).ToListAsync();
            // Configurações do gráfico
            int width = 840;
            int height = 640;
            long[] data = progression.Select(a => a.MemberCount).ToArray(); // Dados do gráfico
            DateTime[] dates = progression.Select(a => a.Date).ToArray(); // Dados do gráfico
            var mudaOAno = dates.Select(a => a.Year).Distinct().Count() > 1;
            long min = (long)(Math.Floor(data.Min()/20M))*20;
            long max = (long)(Math.Ceiling(data.Max()/20M))*20;
            var offsetx = 100;
            var offsety = 20;
            var range = max - min;
            var zeroY = height - 120;
            var yratio = ((float)zeroY) / (float)range;
            float xratio = (width-offsetx) / data.Length;

            using (var bitmap = new SKBitmap(width, height))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    int yscale = 10;
                    using (var paintGrid = new SKPaint())
                    {
                        paintGrid.StrokeWidth = 2;
                        paintGrid.Color = new SKColor(128, 128, 128, 50);
                        paintGrid.IsAntialias = true;
                        var lastX = -50;

                        using var textDatePaint = new SKPaint();
                        textDatePaint.Color = SKColor.Parse("#c0c0c0");
                        textDatePaint.TextSize = 24;
                        textDatePaint.TextAlign = SKTextAlign.Right;
                        textDatePaint.IsAntialias = true;
                        var m = SKMatrix.CreateRotation(45);
                        for (int i = 0; i < data.Length; i++)
                        {
                            var x = offsetx + i * xratio;
                            if (x - lastX >= 40)
                            {
                                canvas.DrawLine(x, offsety, x, zeroY + offsety, paintGrid);
                                //canvas.DrawText($"{dates[i]:dd/MM/yyyy}", x, zeroY + offsety + 30, textDatePaint);
                                SKPath path = new SKPath();
                                path.MoveTo(x - 50, zeroY + offsety + 90);
                                path.LineTo(x + 10, zeroY + offsety + 15);

                                canvas.DrawTextOnPath(mudaOAno ? $"{dates[i]:dd/MM/yy}" : $"{dates[i]:dd/MM}", path, 0, 0, textDatePaint);

                                lastX = (int)x;
                            }
                        }

                        using var textPaint = new SKPaint();
                        textPaint.Color = SKColor.Parse("#c0c0c0");
                        textPaint.TextSize = 24;
                        textPaint.TextAlign = SKTextAlign.Right;
                        textPaint.IsAntialias = true;

                        for (int i = 0; i <= 10; i++)
                        {
                            var y = i * (range / 10) * yratio + offsety;
                            var ytext = min + range - (Math.Ceiling(range / 10M) * i);
                            canvas.DrawText($"{ytext}", 90, y + 8, textPaint);
                            canvas.DrawLine(offsetx, y, (data.Length-1) * xratio + offsetx, y, paintGrid);
                        }
                        canvas.DrawLine(offsetx, zeroY + offsety, (data.Length - 1) * xratio + offsetx, zeroY + offsety, paintGrid);
                    }

                    using (var paint = new SKPaint())
                    {
                        paint.Color = SKColor.Parse("#1de9b6");
                        paint.StrokeWidth = 3;
                        paint.IsAntialias = true;

                        for (int i = 0; i < data.Length - 1; i++)
                        {
                            float x1 = i * xratio;
                            float y1 = ((max - data[i]) * (yratio));
                            float x2 = (i + 1) * xratio;
                            float y2 = ((max - data[i + 1]) * (yratio));
                            
                            if(i == 0)
                                canvas.DrawCircle(new SKPoint(offsetx + x1, offsety + y1), 3, paint);

                            canvas.DrawCircle(new SKPoint(offsetx + x2, offsety + y2), 3, paint);
                            canvas.DrawLine(offsetx + x1, offsety + y1, offsetx + x2, offsety + y2, paint);
                        }
                    }

                    var filename = Path.GetTempFileName();

                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var chart = image.Encode(SKEncodedImageFormat.Png, 100))
                    using (var stream = File.OpenWrite(filename))
                    {
                        chart.SaveTo(stream);
                        return filename;
                    }
                }
            }
        }
    }
}
