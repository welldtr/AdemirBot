using Discord;
using Discord.WebSocket;
using OpenAI.Managers;
using OpenAI;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using DiscordBot.Utils;
using Discord.Interactions;

namespace DiscordBot
{
    internal class Program
    {
        private DiscordShardedClient _client;
        private string? mongoServer = Environment.GetEnvironmentVariable("MongoServer");
        private string? gptKey = Environment.GetEnvironmentVariable("ChatGPTKey");

        public Program()
        {
            CreateProvider();
        }

        private IServiceProvider CreateProvider()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };


            var openAI = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = gptKey!
            });

            var mongo = new MongoClient(mongoServer);
            var db = mongo.GetDatabase("ademir");
            
            _client = new DiscordShardedClient(config);
            var commands = new InteractionService(_client.Rest, new InteractionServiceConfig
            {
                LogLevel = LogSeverity.Info,
                DefaultRunMode = Discord.Interactions.RunMode.Async
            });
            var collection = new ServiceCollection()
               .AddSingleton(db)
               .AddSingleton(config)
               .AddSingleton(_client)
               .AddSingleton(commands)
               .AddSingleton(openAI)
               .AddSingleton<Context>()
               .AddDiscordServices()
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
            await provider.InitializeInteractionModulesAsync();
            var token = Environment.GetEnvironmentVariable("AdemirAuth");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }
    }
}
