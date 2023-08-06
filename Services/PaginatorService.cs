using Discord;
using Discord.WebSocket;

namespace DiscordBot.Services
{
    public class PaginationService
    {
        internal readonly ILogger Log;

        private readonly Dictionary<ulong, PaginatedMessage> _messages;
        private readonly DiscordShardedClient _client;

        public PaginationService(DiscordShardedClient client, ILogger logger = null)
        {
            _messages = new Dictionary<ulong, PaginatedMessage>();
            _client = client;
            _client.ReactionAdded += OnReactionAdded;
            Log = logger;
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

            var message = await channel.SendMessageAsync("", embed: paginated.GetEmbed());

            await message.AddReactionAsync(paginated.Options.EmoteFirst);
            await message.AddReactionAsync(paginated.Options.EmoteBack);
            await message.AddReactionAsync(paginated.Options.EmoteNext);
            await message.AddReactionAsync(paginated.Options.EmoteLast);
            await message.AddReactionAsync(paginated.Options.EmoteStop);

            _messages.Add(message.Id, paginated);
            Log.LogDebug("Listening to message with id {id}");

            if (paginated.Options.Timeout != TimeSpan.Zero)
            {
                var _ = Task.Delay(paginated.Options.Timeout).ContinueWith(async _t =>
                {
                    if (!_messages.ContainsKey(message.Id)) return;
                    if (paginated.Options.TimeoutAction == StopAction.DeleteMessage)
                        await message.DeleteAsync();
                    else if (paginated.Options.TimeoutAction == StopAction.ClearReactions)
                        await message.RemoveAllReactionsAsync();
                    _messages.Remove(message.Id);
                });
            }

            return message;
        }

        internal async Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageParam, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var message = await messageParam.GetOrDownloadAsync();
            if (message == null)
            {
                Log.LogDebug($"Dumped message (not in cache) with id {reaction.MessageId}");
                return;
            }
            if (!reaction.User.IsSpecified)
            {
                Log.LogDebug($"Dumped message (invalid user) with id {message.Id}");
                return;
            }
            if (_messages.TryGetValue(message.Id, out PaginatedMessage page))
            {
                if (reaction.UserId == _client.CurrentUser.Id) return;
                if (page.User != null && reaction.UserId != page.User.Id)
                {
                    Log.LogDebug($"Ignored reaction from user {reaction.UserId}");
                    var _ = message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                    return;
                }
                await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                Log.LogDebug($"Handled reaction {reaction.Emote} from user {reaction.UserId}");
                if (reaction.Emote.Name == page.Options.EmoteFirst.Name)
                {
                    if (page.CurrentPage != 1)
                    {
                        page.CurrentPage = 1;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                }
                else if (reaction.Emote.Name == page.Options.EmoteBack.Name)
                {
                    if (page.CurrentPage != 1)
                    {
                        page.CurrentPage--;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                }
                else if (reaction.Emote.Name == page.Options.EmoteNext.Name)
                {
                    if (page.CurrentPage != page.Count)
                    {
                        page.CurrentPage++;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                }
                else if (reaction.Emote.Name == page.Options.EmoteLast.Name)
                {
                    if (page.CurrentPage != page.Count)
                    {
                        page.CurrentPage = page.Count;
                        await message.ModifyAsync(x => x.Embed = page.GetEmbed());
                    }
                }
                else if (reaction.Emote.Name == page.Options.EmoteStop.Name)
                {
                    if (page.Options.EmoteStopAction == StopAction.DeleteMessage)
                        await message.DeleteAsync();
                    else if (page.Options.EmoteStopAction == StopAction.ClearReactions)
                        await message.RemoveAllReactionsAsync();
                    _messages.Remove(message.Id);
                }
            }
        }
    }

    public static class PaginationExtensions
    {
        [Obsolete("Addon builders on a client are discouraged, consider IServiceCollection.AddPaginator()")]
        public static DiscordShardedClient UsePaginator(this DiscordShardedClient client, IServiceCollection collection, ILogger logger = null)
        {
            collection.AddSingleton(new PaginationService(client, logger));
            return client;
        }
        /// <summary>
        /// Adds a PaginationService to a ServiceCollection
        /// </summary>
        /// <param name="collection">The service collection</param>
        /// <param name="client">The client this paginator will use</param>
        /// <param name="logger">A logging delegate</param>
        /// <returns>The service collection, with the pagiantor appended to it (for fluent patterns)</returns>
        public static IServiceCollection AddPaginator(this IServiceCollection collection, DiscordShardedClient client, ILogger logger = null)
        {
            collection.AddSingleton(new PaginationService(client, logger));
            return collection;
        }
        /// <summary>
        /// Adds a PaginationService to a ServiceCollection, assuming a DiscordShardedClient is already present in the collection, and that no logging method is wanted.
        /// </summary>
        /// <param name="collection">The service collection.</param>
        /// <returns>The service collection, with the pagiantor appended to it (for fluent patterns)</returns>
        public static IServiceCollection AddPaginator(this IServiceCollection collection)
        {
            collection.AddSingleton<PaginationService>();
            return collection;
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
        public const string FIRST = "⏮";
        public const string BACK = "◀";
        public const string NEXT = "▶";
        public const string LAST = "⏭";
        public const string STOP = "⏹";

        public IEmote EmoteFirst { get; set; } = new Emoji(FIRST);
        public IEmote EmoteBack { get; set; } = new Emoji(BACK);
        public IEmote EmoteNext { get; set; } = new Emoji(NEXT);
        public IEmote EmoteLast { get; set; } = new Emoji(LAST);
        public IEmote EmoteStop { get; set; } = new Emoji(STOP);
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
