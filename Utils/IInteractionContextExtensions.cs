using Discord;
using Discord.WebSocket;
using OpenAI.ObjectModels.RequestModels;
using System.Text.RegularExpressions;
using System.Text;

namespace DiscordBot.Utils
{
    public static class IInteractionContextExtensions
    {
        public static bool IsPremium(this IGuild guild)
        {
            var premiumGuilds = Environment.GetEnvironmentVariable("PremiumGuilds")!.Split(',');
            return premiumGuilds.Contains(guild.Id.ToString());
        }

        public static async Task<string> GetGPTAuthorNameAsync(this IMessage msg)
        {
            var channel = (ITextChannel)msg.Channel;
            var author = await channel.GetUserAsync(msg?.Author?.Id ?? 0);
            var me = await channel.Guild.GetCurrentUserAsync();
            var name = msg!.Author.Id == me.Id ? "Ademir" : author.DisplayName;
            return name.AsAlphanumeric();
        }

        public static async Task<IUserMessage> SendEmbedText(this ITextChannel channel, string text, string desc = default)
        {
            var embed = new EmbedBuilder()
                   .WithAuthor(text);

            if(!string.IsNullOrEmpty(desc))
                embed = embed.WithDescription(desc);

            return await channel.SendMessageAsync(embed: embed.Build());
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
                channel = (ITextChannel) await channel.Guild.GetChannelAsync(msg.Reference.ChannelId);

            var msgRefer = await channel.GetMessageAsync(msg.Reference.MessageId.Value!);
            return msgRefer;
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
                if (message.Type == MessageType.Default)
                    msgs.Insert(0, new ChatMessage(autor, message.Content.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
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
                if (m.Type == MessageType.Default)
                    msgs.Insert(0, new ChatMessage(autor, m.Content.Replace($"<@{_client.CurrentUser.Id}>", "Ademir"), nome));
            }

            var firstMsg = msgsThread.LastOrDefault();
            if (firstMsg != null)
            {
                var ch = (ITextChannel)guild.GetChannel(firstMsg?.Reference?.ChannelId ?? message.Channel.Id);
                await _client.GetRepliedMessages(ch, firstMsg, msgs);
            }
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
                        mm = await channel.SendMessageAsync(resposta.Substring(start), messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                    }
                }
                else
                {
                    foreach (var trecho in trechos)
                    {
                        mm = await channel.SendMessageAsync(trecho, messageReference: msgRefer, allowedMentions: AllowedMentions.None);
                        msgRefer = (channel is IThreadChannel ? msgRefer : new MessageReference(mm.Id));
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
