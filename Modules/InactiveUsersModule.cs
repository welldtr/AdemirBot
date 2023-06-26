using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Domain.Entities;
using MongoDB.Driver;
using System.Text;

namespace DiscordBot.Modules
{
    public class InactiveUsersModule : InteractionModuleBase
    {
        private readonly DiscordShardedClient _client;
        private readonly Context db;
        private bool importando = false;

        public InactiveUsersModule(DiscordShardedClient client, Context context)
        {
            _client = client;
            db = context;
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("importar-historico-mensagens", "Importa mensagens do histórico até 365 dias")]
        public async Task ImportarHistorico(
            [Summary(description: "Canal a analisar")] ITextChannel canal)
        {
            try
            {
                if (importando)
                {
                    await RespondAsync("Importação de histórico de já iniciada anteriormente", ephemeral: false);
                }

                importando = true;
                await DeferAsync();
                var guildId = Context.Guild.Id;
               
                var earlierMessage = await db.messagelog.Find(a => a.ChannelId == canal.Id && a.MessageLength > 0)
                    .SortBy(a => a.MessageDate)
                    .FirstOrDefaultAsync();

                var eagerMessage = await canal.GetMessagesAsync(1).Flatten().FirstOrDefaultAsync();
                IEnumerable<IMessage> messages = null;

                if (earlierMessage != null)
                {
                    messages = new[] { await canal.GetMessageAsync(earlierMessage.MessageId) };
                }
                else if (eagerMessage != null)
                {
                    messages = new[] { eagerMessage };
                }
                else
                {
                    await ModifyOriginalResponseAsync(a => a.Content = "Canal vazio.");
                    return;
                }

                await ModifyOriginalResponseAsync(a => a.Content = "Importação de histórico de mensagens iniciada");

                var msg = messages.LastOrDefault();
                if (msg != null)
                {
                    var memberid = (msg.Author?.Id ?? 0);
                    await db.messagelog.UpsertAsync(new Message
                    {
                        MessageId = msg.Id,
                        ChannelId = canal.Id,
                        GuildId = guildId,
                        MessageDate = msg.Timestamp.UtcDateTime,
                        UserId = memberid,
                        MessageLength = msg.Content.Length
                    });
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (messages.Count() > 0 && messages.Last().Timestamp.UtcDateTime >= DateTime.Today.AddDays(-765))
                        {
                            messages = await canal
                                        .GetMessagesAsync(messages.Last(), Direction.Before, 500)
                                        .FlattenAsync();

                            if (messages.Count() == 0)
                                break;

                            Console.WriteLine($"Processando o dia {messages.Last().Timestamp.UtcDateTime:dd/MM/yyyy}");

                            await ModifyOriginalResponseAsync(a =>
                            {
                                a.Flags = MessageFlags.Loading;
                                a.Content = $"Importando mensagens de {canal.Name} do dia {msg.Timestamp.UtcDateTime:dd/MM/yyyy HH:mm}";
                            });

                            foreach (var msg in messages)
                            {
                                var memberid = (msg.Author?.Id ?? 0);
                                await db.messagelog.UpsertAsync(new Message
                                {
                                    MessageId = msg.Id,
                                    ChannelId = canal.Id,
                                    GuildId = Context.Guild.Id,
                                    MessageDate = msg.Timestamp.UtcDateTime,
                                    UserId = memberid,
                                    MessageLength = msg.Content.Length
                                });
                                Console.Write(".");
                            }
                        }

                        await ModifyOriginalResponseAsync(a => a.Content = $"Importação de histórico de mensagens do {canal.Name} terminada.");
                    }
                    catch (Exception ex)
                    {
                        await ModifyOriginalResponseAsync(a =>
                        {
                            a.Flags = MessageFlags.Loading;
                            a.Content = $"Erro ao importar mensagens de {canal.Name}: {ex}";
                        });
                        Console.WriteLine(ex.ToString());
                    }
                    importando = false;
                });
            }
            catch
            {
                importando = false;
            }
            importando = false;
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("usuarios-inativos", "Extrair uma lista dos usuários que menos escrevem no chat.")]
        public async Task UsuariosInativos(
            [Summary(description: "Canal a analisar")] IChannel canal)
        {
            var guildId = Context.Guild.Id;
            var guild = _client.Guilds.First(a => a.Id == guildId);
            var admin = _client.Guilds.First(a => a.Id == Context.Guild.Id)
                .GetUser(Context.User.Id).GuildPermissions.Administrator;

            if (!admin)
            {
                await RespondAsync("Apenas administradores podem configurar o canal de denuncias.", ephemeral: true);
                return;
            }

            var usuarios = guild.Users.Where(a => !a.IsBot);
            var rankMsg = new Dictionary<SocketGuildUser, DateTime>();

            await DeferAsync();

            var csv = new StringBuilder();

            var filePath = $"./{Guid.NewGuid()}.csv";
            csv.AppendLine("ID;Username;Nickname;Message Count;Last Interaction;Joined At");
            foreach (var user in usuarios)
            {
                var query = db.messagelog
                        .Find(a => a.UserId == user.Id)
                        .SortByDescending(a => a.MessageDate);

                var count = await query.CountDocumentsAsync();
                var lastmessage = await query.FirstOrDefaultAsync();

                var newLine = $"\"\"\"\"{user.Id}\";{user.Username.Replace(";", "\",\"")};{(user.Nickname?.Replace(";", "\",\"") ?? user.Username.Replace(";", "\",\""))};{count};{lastmessage?.MessageDate:dd/MM/yyyy HH:mm};{user?.JoinedAt:dd/MM/yyyy HH:mm}";
                csv.AppendLine(newLine);
            }

            await File.WriteAllTextAsync(filePath, csv.ToString());

            await ModifyOriginalResponseAsync(a =>
            {
                a.Content = "Relatório de Usuários ordenados por data de ultima interação.";
                a.Attachments = new[] { new FileAttachment(filePath) };
            });
        }
    }
}
