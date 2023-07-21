using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MongoDB.Driver;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI.Managers;
using DiscordBot.Utils;
using System.Text;
using DiscordBot.Domain.Entities;

namespace DiscordBot.Modules
{
    public class ChatGPTModule : InteractionModuleBase
    {
        private readonly OpenAIService openAI;
        private readonly Context db;

        public ChatGPTModule(OpenAIService openAI, Context ctx)
        {
            this.openAI = openAI;
            this.db = ctx;
        }

        [SlashCommand("dall-e", "Pedir ao Dall-e uma imagem com a descrição.")]
        public async Task DallE([Summary(description: "Comando do DALL-E")] string comando)
        {
            var guild = ((SocketTextChannel)Context.Channel).Guild;
            var me = guild.Users.First(a => a.Id == Context.User.Id);
            if (guild.Id != 1055161583841595412 && !(Context.Guild.IsPremium() && me.PremiumSince.HasValue))
            {
                await RespondAsync($"Funcionalidade premium. Booste o servidor {guild.Name} para usar.", ephemeral: true);
                return;
            }

            await DeferAsync();
            var imageResult = await openAI.Image.CreateImage(new ImageCreateRequest
            {
                Prompt = comando!,
                N = 1,
                Size = StaticValues.ImageStatics.Size.Size512,
                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
            });

            var attachments = new List<FileAttachment>();
            if (imageResult.Successful)
            {
                foreach (var img in imageResult.Results)
                {
                    var stream = await new HttpClient().GetStreamAsync(img.Url);
                    attachments.Add(new FileAttachment(stream, $"{Context.Interaction.Id}.jpg"));
                }
                await ModifyOriginalResponseAsync(a =>
                {
                    a.Content = comando;
                    a.Attachments = attachments.ToArray();
                });
            }
            else
            {
                await ModifyOriginalResponseAsync(a => a.Content = $"Erro ao processar o comando \"{comando}\"");
            }
        }

        [SlashCommand("completar", "Pedir ao GPT uma completude de texto.")]
        public async Task GPTText([Summary(description: "Comando")] string comando)
        {
            var guild = ((SocketTextChannel)Context.Channel).Guild;
            var me = guild.Users.First(a => a.Id == Context.User.Id);
            if (guild.Id != 1055161583841595412 && !(Context.Guild.IsPremium() && (me.PremiumSince.HasValue || me.GuildPermissions.Administrator)))
            {
                await RespondAsync($"Funcionalidade premium. Booste o servidor {guild.Name} para usar.", ephemeral: true);
                return;
            }

            await DeferAsync();

            var imageResult = await openAI.Completions.CreateCompletion(new CompletionCreateRequest
            {
                Prompt = comando!,
                N = 1,
                MaxTokens = 1000,
                Model = Models.TextDavinciV3,
                Temperature = 0.9f
            });

            var msg = await ((SocketSlashCommand)Context.Interaction).GetOriginalResponseAsync();

            if (imageResult.Successful)
            {
                foreach (var choice in imageResult.Choices)
                {
                    await ModifyOriginalResponseAsync(a => a.Content = $"Comando: \"{comando}\"");
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(choice.Text));
                    await Context.Channel.SendFileAsync(new FileAttachment(ms, "Resposta.txt"), messageReference: new MessageReference(msg.Id));
                }
            }
            else
            {
                await ModifyOriginalResponseAsync(a => a.Content = $"Erro ao processar o comando \"{comando}\"");
            }
        }

        [SlashCommand("thread", "Criar uma tread privada com o Ademir.")]
        public async Task NewThread([Summary(description: "Nome da nova Thread")] string nome)
        {
            var guild = ((SocketTextChannel)Context.Channel).Guild;
            var me = guild.Users.First(a => a.Id == Context.User.Id);
            if (guild.Id != 1055161583841595412 && !(Context.Guild.IsPremium() && (me.PremiumSince.HasValue || me.GuildPermissions.Administrator)))
            {
                await RespondAsync($"Funcionalidade premium. Booste o servidor {guild.Name} para usar.", ephemeral: true);
                return;
            }

            await DeferAsync();

            var channel = await ((ITextChannel)Context.Channel).CreateThreadAsync(nome, ThreadType.PublicThread);

            await db.threads.UpsertAsync(new ThreadChannel
            {
                ThreadId = channel.Id,
                GuildId = channel.Guild.Id,
                MemberId = Context.Client.CurrentUser.Id,
                LastMessageTime = channel.CreatedAt.UtcDateTime,
            });
            await ((ISlashCommandInteraction)Context.Interaction).DeleteOriginalResponseAsync();
        }

        [SlashCommand("restart-thread", "Apagar as msgs de uma tread privada com o Ademir.")]
        public async Task RestartThread()
        {
            IThreadChannel? ch = Context.Channel as IThreadChannel;
            var me = await Context.Guild.GetUserAsync(Context.User.Id);
            if(ch == null)
            {
                await RespondAsync("Você não está em uma thread com o Ademir.");
                return;
            }

            var msgs = (await ch.GetMessagesAsync(ch.MessageCount).FlattenAsync()).OrderBy(m => m.Timestamp);
            if(msgs.FirstOrDefault()?.Author.Id != Context.User.Id && !me.GuildPermissions.Administrator)
            {
                await RespondAsync("Você precisa ser o solicitante da thread.");
                return;
            }

            await DeferAsync();
            foreach (var msg in msgs)
            {
                await ch.DeleteMessageAsync(msg.Id);
            }

            await ((ISlashCommandInteraction)Context.Interaction).DeleteOriginalResponseAsync();
        }
    }
}
