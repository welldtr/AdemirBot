using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MongoDB.Driver;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI.Managers;
using DiscordBot.Utils;

namespace DiscordBot.Modules
{
    public class ChatGPTModule : InteractionModuleBase
    {
        private readonly OpenAIService openAI;

        public ChatGPTModule(OpenAIService openAI)
        {
            this.openAI = openAI;
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

        [SlashCommand("thread", "Criar uma tread privada com o Ademir.")]
        public async Task NewThread([Summary(description: "Nome da nova Thread")] string nome)
        {
            var guild = ((SocketTextChannel)Context.Channel).Guild;
            var me = guild.Users.First(a => a.Id == Context.User.Id);
            if (guild.Id != 1055161583841595412 && !(Context.Guild.IsPremium() && me.PremiumSince.HasValue))
            {
                await RespondAsync($"Funcionalidade premium. Booste o servidor {guild.Name} para usar.", ephemeral: true);
                return;
            }

            await DeferAsync();

            await ((ITextChannel)Context.Channel).CreateThreadAsync(nome, ThreadType.PublicThread);
        }
    }
}
