using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Domain.Enum;

namespace DiscordBot.Services
{
    public class PaginationService
    {
        internal readonly ILogger Log;
        private readonly Dictionary<ulong, PaginatedMessage> _messages;
        private readonly DiscordShardedClient _client;

        public PaginationService(DiscordShardedClient client, ILogger<PaginationService> logger = null)
        {
            _messages = new Dictionary<ulong, PaginatedMessage>();
            _client = client;
            //_client.ReactionAdded += OnReactionAdded;
            Log = logger;
        }

        public void InitInteractionServicePagination(InteractionService service)
        {
            service.ComponentCommandExecuted += _interactionService_InteractionExecuted;
        }
        /// <summary>
        /// Sends a paginated message (with reaction buttons)
        /// </summary>
        /// <param name="channel">The channel this message should be sent to</param>
        /// <param name="paginated">A <see cref="PaginatedMessage">PaginatedMessage</see> containing the pages.</param>
        /// <exception cref="Net.HttpException">Thrown if the bot user cannot send a message or add reactions.</exception>
        /// <returns>The paginated message.</returns>
        public async Task<IUserMessage> SendPaginatedMessageAsync(IMessageChannel channel, PaginatedMessage paginated)
        {
            Log.LogInformation($"Sending message to {channel}");

            var message = await channel.SendMessageAsync(" ", embed: paginated.GetEmbed());

            var msgid = message.Id;
            var components = new ComponentBuilder()
                .WithButton(null, $"first-page:~{msgid}", ButtonStyle.Primary, PlayerEmote.Back)
                .WithButton(null, $"back-page:~{msgid}", ButtonStyle.Primary, PlayerEmote.Rewind)
                .WithButton(null, $"next-page:~{msgid}", ButtonStyle.Primary, PlayerEmote.Forward)
                .WithButton(null, $"last-page:~{msgid}", ButtonStyle.Primary, PlayerEmote.Skip)
                .WithButton(null, $"stop-paging:~{msgid}", ButtonStyle.Primary, PlayerEmote.Stop)
                .Build();
            await message.ModifyAsync(a => a.Components = components);
            
            _messages.Add(message.Id, paginated);
            Log.LogDebug($"Listening to message with id {msgid}");

            if (paginated.Options.Timeout != TimeSpan.Zero)
            {
                var _ = Task.Delay(paginated.Options.Timeout).ContinueWith(async _t =>
                {
                    if (!_messages.ContainsKey(message.Id)) return;
                    if (paginated.Options.TimeoutAction == StopAction.DeleteMessage)
                        await message.DeleteAsync();
                    else if (paginated.Options.TimeoutAction == StopAction.ClearReactions)
                        await message.ModifyAsync(a => a.Components = new ComponentBuilder().Build());
                    _messages.Remove(message.Id);
                });
            }
            return message;
        }

        private async Task _interactionService_InteractionExecuted(ICommandInfo cmd, IInteractionContext ctx, Discord.Interactions.IResult res)
        {
            var componentSplit = ((SocketMessageComponent)ctx.Interaction).Data.CustomId.Split(":~");

            if (componentSplit.Length != 2)
                return;

            var commandName = componentSplit[0];
            var messageId = ulong.Parse(componentSplit[1]);
            var guild = ctx.Guild;
            if (guild == null)
                return;
            var channel = ctx.Channel;

            if (channel == null)
                return;

            var message = (IUserMessage)await channel.GetMessageAsync(messageId);
            if (message == null)
            {
                Log.LogDebug($"Dumped message (not in cache) with id {commandName}");
                return;
            }

            if (ctx.User == null)
            {
                Log.LogDebug($"Dumped message (invalid user) with id {message.Id}");
                return;
            }

            if (_messages.TryGetValue(messageId, out PaginatedMessage page))
            {
                if (ctx.User.Id == _client.CurrentUser.Id) return;
                if (page.User != null && ctx.User.Id != page.User.Id)
                {
                    Log.LogDebug($"Ignored reaction from user {ctx.User.Id}");
                    return;
                }

                Log.LogDebug($"Handled reaction {commandName} from user {ctx.User.Id}");
                if (commandName == "first-page")
                {
                    if (page.CurrentPage != 1)
                    {
                        page.CurrentPage = 1;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                    await ctx.Interaction.DeferAsync();
                }
                else if (commandName == "back-page")
                {
                    if (page.CurrentPage != 1)
                    {
                        page.CurrentPage--;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                    await ctx.Interaction.DeferAsync();
                }
                else if (commandName == "next-page")
                {
                    if (page.CurrentPage != page.Count)
                    {
                        page.CurrentPage++;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                    await ctx.Interaction.DeferAsync();
                }
                else if (commandName == "last-page")
                {
                    if (page.CurrentPage != page.Count)
                    {
                        page.CurrentPage = page.Count;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                    await ctx.Interaction.DeferAsync();
                }
                else if (commandName == "stop-paging")
                {
                    if (page.Options.EmoteStopAction == StopAction.DeleteMessage)
                        await message.DeleteAsync();
                    else if (page.Options.EmoteStopAction == StopAction.ClearReactions)
                        await message.RemoveAllReactionsAsync();
                    _messages.Remove(message.Id);
                    await ctx.Interaction.DeferAsync();
                }
            }
        }
    }

    public class PaginatedMessage
    {
        public PaginatedMessage(IEnumerable<string> pages, string title = "", Color? embedColor = null, IUser user = null, AppearanceOptions options = null)
            => new PaginatedMessage(pages.Select(x => new Page { Description = x }), title, embedColor, user, new AppearanceOptions { });
        public PaginatedMessage(IEnumerable<Page> pages, string title = "", Color? embedColor = null, IUser user = null, AppearanceOptions options = null)
        {
            var embeds = new List<Embed>();
            int i = 1;
            foreach (var page in pages)
            {
                var builder = new EmbedBuilder()
                    .WithColor(embedColor ?? Color.Default)
                    .WithTitle(title)
                    .WithDescription(page?.Description ?? "")
                    .WithImageUrl(page?.ImageUrl ?? "")
                    .WithThumbnailUrl(page?.ThumbnailUrl ?? "")
                    .WithFooter(footer =>
                    {
                        footer.Text = $"Page {i++}/{pages.Count()}";
                    });
                builder.Fields = page.Fields?.ToList();
                embeds.Add(builder.Build());
            }
            Pages = embeds;
            Title = title;
            EmbedColor = embedColor ?? Color.Default;
            User = user;
            Options = options ?? new AppearanceOptions();
            CurrentPage = 1;
        }

        internal Embed GetEmbed()
        {
            return Pages.ElementAtOrDefault(CurrentPage - 1);
        }

        internal string Title { get; }
        internal Color EmbedColor { get; }
        internal IReadOnlyCollection<Embed> Pages { get; }
        internal IUser User { get; }
        internal AppearanceOptions Options { get; }
        internal int CurrentPage { get; set; }
        internal int Count => Pages.Count;
    }

    public class AppearanceOptions
    {
        public IEmote EmoteFirst { get; set; } = PlayerEmote.Back;
        public IEmote EmoteBack { get; set; } = PlayerEmote.Rewind;
        public IEmote EmoteNext { get; set; } = PlayerEmote.Forward;
        public IEmote EmoteLast { get; set; } = PlayerEmote.Skip;
        public IEmote EmoteStop { get; set; } = PlayerEmote.Stop;
        public TimeSpan Timeout { get; set; } = TimeSpan.Zero;
        public StopAction EmoteStopAction { get; set; } = StopAction.DeleteMessage;
        public StopAction TimeoutAction { get; set; } = StopAction.DeleteMessage;
    }

    public enum StopAction
    {
        ClearReactions,
        DeleteMessage
    }

    public class Page
    {
        public IReadOnlyCollection<EmbedFieldBuilder> Fields { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string ThumbnailUrl { get; set; }
    }
}
