using Discord;
using Discord.WebSocket;
using OpenAI.ObjectModels.RequestModels;
using System.Text.RegularExpressions;
using System.Text;
using OpenAI.ObjectModels;
using System.Collections.Concurrent;

namespace DiscordBot.Utils
{
    public static class IInteractionContextExtensions
    {
        private static ConcurrentDictionary<ulong, bool> premiumGuilds = new ConcurrentDictionary<ulong, bool>();

        public static bool IsPremium(this IGuild guild)
        {
            return premiumGuilds[guild.Id];
        }
        public static void SetPremium(this IGuild guild,bool premium)
        {
            if (premiumGuilds[guild.Id] != premium)
                premiumGuilds[guild.Id] = premium;
        }

        public static async Task<string> GetGPTAuthorNameAsync(this IMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            var author = await channel.GetUserAsync(msg?.Author?.Id ?? 0);
            var authorBestName = author.DisplayName.Matches("^[a-zA-Z0-9_-]{1,64}$") ? author.DisplayName : author.Username;
            var me = await channel.Guild.GetCurrentUserAsync();
            var name = msg!.Author.Id == me.Id ? "Ademir" : authorBestName;
            return name.AsAlphanumeric();
        }

        public static async Task<IUserMessage> SendEmbedText(this ITextChannel channel, string text)
        {
            var embed = new EmbedBuilder()
                   .WithDescription(text);

            return await channel.SendMessageAsync(embed: embed.Build());
        }

        public static async Task<IUserMessage> SendEmbedText(this ITextChannel channel, string title, string text)
        {
            var embed = new EmbedBuilder()
                   .WithDescription(text);

            if (!string.IsNullOrEmpty(title))
                embed = embed.WithAuthor(title);

            return await channel.SendMessageAsync(embed: embed.Build());
        }

        public async static Task EnsureUserCanUseThePlayer(this IGuildUser user, ITextChannel channel, Func<Task> funcao)
        {
            if (CanUserCommandBot(user, channel))
            {
                await funcao();
            }
            else
            {
                await channel.SendEmbedText("Você não pode controlar o player de áudio nesse momento. Aguarde até que os outros membros terminem de usar.");
                return;
            }
        }

        public async static Task EnsureUserCanUseThePlayer(this IInteractionContext ctx, Func<IGuildUser, ITextChannel, Task> funcao)
        {
            await ctx.Interaction.DeferAsync();
            var user = (IGuildUser)ctx.User;
            var channel = (ITextChannel)ctx.Channel;
            if (CanUserCommandBot(user, channel))
            {
                await funcao(user, channel);
            }
            else
            {
                await channel.SendEmbedText("Você não pode controlar o player de áudio nesse momento. Aguarde até que os outros membros terminem de usar.");
            }

            if (ctx is Discord.Interactions.ShardedInteractionContext cmd && cmd.Interaction is ISlashCommandInteraction)
            {
                await cmd.Interaction.DeleteOriginalResponseAsync();
            }
        }

        private static bool CanUserCommandBot(IGuildUser user, ITextChannel channel)
        {
            var playback = channel.GetPlayback();
            if (playback.VoiceChannel == null || playback.VoiceChannel.Id == channel.Guild.AFKChannelId)
                return true;

            var usersConnectedWithBot = ((SocketVoiceChannel)playback.VoiceChannel).ConnectedUsers.Where(a => !a.IsBot);
            if (usersConnectedWithBot.Count() == 0)
                return true;

            return usersConnectedWithBot.Any(a => a.Id == user.Id);
        }

        public static ITextChannel GetTextChannel(this SocketMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            return channel;
        }

        public static async Task<IGuildUser> GetAuthorGuildUserAsync(this SocketMessage msg)
        {
            var channel = msg.GetTextChannel();
            var author = await channel.Guild.GetUserAsync(msg.Author.Id);
            return author;
        }

        public static ulong GetGuildId(this SocketMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            return channel.GuildId;
        }

        public static async Task<string> GetGPTAuthorRoleAsync(this IMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            var me = await channel.Guild.GetCurrentUserAsync();
            var role = (msg.Author.Id == me.Id) ? "assistant" : "user";
            return role;
        }
        public static async Task<IMessage> GetReferenceAsync(this IMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            if (msg.Channel.Id != msg.Reference.ChannelId)
                channel = (ITextChannel)await channel.Guild.GetChannelAsync(msg.Reference.ChannelId);

            var msgRefer = await channel.GetMessageAsync(msg.Reference.MessageId.Value!);
            return msgRefer;
        }

        public static async Task<string> GetMessageContentWithAttachments(this IMessage msg)
        {
            var urlAnexoSemImagem = msg.Attachments.FirstOrDefault(a => !a.ContentType.Contains("image"))?.Url;
            var temImagem = msg.Attachments.Any(a => a.ContentType.Contains("image"));

            if (string.IsNullOrEmpty(urlAnexoSemImagem) && temImagem)
            {
                return "(imagem)";
            }
            else
            {
                var conteudoAnexo = string.IsNullOrEmpty(urlAnexoSemImagem) ? "" : await new HttpClient().GetStringAsync(urlAnexoSemImagem);
                var attachmentContent = conteudoAnexo;
                var content = (msg.Content + attachmentContent);
                return content;
            }
        }

        public static async Task GetRepliedMessages(this DiscordShardedClient _client, ITextChannel channel, IMessage message, List<ChatMessage> msgs)
        {
            var guild = _client.GetGuild(channel.GuildId);
            while (message.Reference != null && message.Reference.MessageId.IsSpecified)
            {
                if (channel.Id != message.Reference.ChannelId)
                    channel = (ITextChannel)guild.GetChannel(message.Reference.ChannelId);

                message = await message.GetReferenceAsync();
                var autor = await message.GetGPTAuthorRoleAsync();
                var nome = await message.GetGPTAuthorNameAsync();

                var content = await message.GetMessageContentWithAttachments();
                msgs.Insert(0, new ChatMessage(autor, content?.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
            }
        }

        public static async Task GetThreadMessages(this DiscordShardedClient _client, IThreadChannel thread, IMessage message, List<ChatMessage> msgs)
        {
            var guild = _client.GetGuild(thread.GuildId);

            var msgsThread = await thread.GetMessagesAsync(message.Id, Direction.Before).FlattenAsync();
            foreach (var m in msgsThread)
            {
                var autor = await m.GetGPTAuthorRoleAsync();
                var nome = await m.GetGPTAuthorNameAsync();

                var content = await m.GetMessageContentWithAttachments();

                var gptTokenLimit = 4000;

                if (guild.Id == 1055161583841595412)
                {
                    var qtd = OpenAI.Tokenizer.GPT3.TokenizerGpt3.TokenCount(string.Join("\n", msgs.Select(a => a.Content)));
                    gptTokenLimit = 128000;
                }

                if (message.Type == MessageType.Default)
                {
                    if (OpenAI.Tokenizer.GPT3.TokenizerGpt3.TokenCount(content) < gptTokenLimit)
                        msgs.Insert(0, new ChatMessage(autor, content.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
                    else
                        msgs.Insert(0, new ChatMessage("system", $"O usuário mandou uma mensagem maior que {gptTokenLimit} tokens."));
                }
            }

            var firstMsg = msgsThread.LastOrDefault();
            if (firstMsg != null)
            {
                var ch = (ITextChannel)guild.GetChannel(firstMsg?.Reference?.ChannelId ?? message.Channel.Id);
                await _client.GetRepliedMessages(ch, firstMsg, msgs);
            }
        }

        public static async Task<string?> GetWelcomeDescriptionScreenAsync(this SocketGuild guild)
        {
            try
            {
                var welcomeScreen = await guild.GetWelcomeScreenAsync();
                return welcomeScreen.Description;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static string[] SplitInChunksOf(this string text, int maxChars)
        {
            var linesFinal = new List<string>();
            var sb = new StringBuilder();
            foreach (var line in text.Split(new char[] { '\r', '\n' }))
            {
                if (sb.Length + line.Length > maxChars)
                {
                    linesFinal.Add(sb.ToString());
                    sb.Clear();
                }
                sb.Append(line);
            }
            linesFinal.Add(sb.ToString());
            return linesFinal.ToArray();
        }

        public static async Task<IUserMessage> Responder(this IMessageChannel channel, string resposta, MessageReference msgRefer)
        {
            var trechos = resposta.Split("\n\n");
            IUserMessage mm = null;
            if (resposta.Length >= 2000)
            {
                if (resposta.Contains("```"))
                {
                    var start = 0;
                    MatchCollection textmatches = Regex.Matches(resposta, @"```(?'lang'\S*)\s*(?'code'[\s\S]*?)\s*```", RegexOptions.Singleline);
                    foreach (Match match in textmatches)
                    {
                        var substr = match.Index - start;
                        var prevText = resposta.Substring(start, substr);
                        start = match.Index + match.Length;
                        var lang = match.Groups["lang"].Value;
                        var code = match.Groups["code"].Value;
                        string trecho = $"```{lang}\n{code}```";

                        if (!string.IsNullOrWhiteSpace(prevText))
                        {
                            mm = await channel.SendMessageAsync(prevText, messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                            msgRefer = (channel is IThreadChannel ? msgRefer : new MessageReference(mm.Id));
                        }

                        if (trecho.Length > 2000)
                        {
                            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(code));
                            mm = await channel.SendFileAsync(new FileAttachment(fileStream, $"message.{lang}"));
                        }
                        else
                        {
                            mm = await channel.SendMessageAsync(trecho, messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                        }
                    }
                    if (start < resposta.Length - 1)
                    {
                        var textoRestante = resposta.Substring(start);
                        if (textoRestante.Length > 2000)
                        {
                            var sb = new StringBuilder();
                            foreach (var line in textoRestante.Split(new char[] { '\r', '\n' }))
                            {
                                if (sb.Length + line.Length < 2000)
                                {
                                    sb.AppendLine(line);
                                }
                                else
                                {
                                    await channel.SendMessageAsync(sb.ToString(), messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                                    sb.Clear();
                                }
                            }
                            if (sb.Length > 0)
                            {
                                await channel.SendMessageAsync(sb.ToString(), messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                            }
                        }
                        else
                        {
                            mm = await channel.SendMessageAsync(textoRestante, messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                        }
                    }
                }
                else
                {
                    foreach (var trecho in trechos)
                    {
                        if (!string.IsNullOrWhiteSpace(trecho))
                        {
                            mm = await channel.SendMessageAsync(trecho, messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                            msgRefer = (channel is IThreadChannel ? msgRefer : new MessageReference(mm.Id));
                        }
                    }
                }
            }
            else
            {
                mm = await channel.SendMessageAsync(resposta, messageReference: msgRefer, allowedMentions: AllowedMentions.None);
            }
            return mm;
        }
    }
}
