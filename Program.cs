using Discord;
using Discord.WebSocket;
using OpenAI.Managers;
using OpenAI;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using DiscordBot.Utils;
using DiscordBot.Services;

namespace DiscordBot
{
    internal class Program
    {
        private DiscordShardedClient _client;
        private ILogger<Program> _log;
        private string? mongoServer = Environment.GetEnvironmentVariable("MongoServer");
        private string? gptKey = Environment.GetEnvironmentVariable("ChatGPTKey");

        public Program()
        {
            _serviceProvider = CreateProvider();
        }

        private IServiceProvider CreateProvider()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            var commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
            });

            var openAI = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = gptKey!
            });

            var mongo = new MongoClient(mongoServer);
            var db = mongo.GetDatabase("ademir");

            var collection = new ServiceCollection()
               .AddSingleton(db)
               .AddSingleton(config)
               .AddSingleton(commands)
               .AddSingleton(openAI)
               .AddSingleton<DiscordShardedClient>()
               .AddSingleton<Context>()
               .AddSingleton<AudioService>()
               .AddSingleton<BumpRewardService>()
               .AddSingleton<ChatGPTAssistantService>()
               .AddSingleton<MacroService>()
               .AddSingleton<GuildPolicyService>()
               .AddLogging(b =>
               {
                   b.AddConsole();
                   b.SetMinimumLevel(LogLevel.Information);
               });

            return collection.BuildServiceProvider();
        }

        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            //await aternosClient.StartServer();

            var provider = CreateProvider();
            var commands = provider.GetRequiredService<CommandService>();
            _client = provider.GetRequiredService<DiscordShardedClient>();
            _log = provider.GetRequiredService<ILogger<Program>>();
            await provider.InitializeInteractionModulesAsync();

            _client.ShardReady += async (shard) =>
            {
                await _client.SetGameAsync($"tudo e todos [{shard.ShardId}]", type: ActivityType.Listening);
                _log.LogInformation($"Shard Number {shard.ShardId} is connected and ready!");
            };

            var token = Environment.GetEnvironmentVariable("AdemirAuth");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }
    }
}
