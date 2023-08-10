using Discord;
using Discord.WebSocket;
using OpenAI.Managers;
using OpenAI;
using MongoDB.Driver;
using DiscordBot.Utils;
using Discord.Interactions;
using DiscordBot.Services;
using System.Reflection;

namespace DiscordBot
{
    internal class Program
    {
        private DiscordShardedClient _client;
        private string? mongoServer { get => Environment.GetEnvironmentVariable("MongoServer"); }
        private string? gptKey { get => Environment.GetEnvironmentVariable("ChatGPTKey"); }


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
               .AddSingleton((s) => new PaginationService(_client, s.GetRequiredService<ILogger<PaginationService>>()))
               .AddSingleton(commands)
               .AddSingleton(openAI)
               .AddSingleton<Context>()
               .AddSingleton<Context>()
               .AddDiscordServices()
               .AddMemoryCache()
               .AddLogging(b =>
               {
                   b.AddConsole();
                   b.SetMinimumLevel(LogLevel.Information);
               });

            return collection.BuildServiceProvider();
        }

        public static Task Main(string[] args) => new Program().MainAsync(args);

        public async Task MainAsync(string[] args)
        {
            DotEnv.Load();
            CreateProvider();
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/", () => "Hello World!");
            var provider = CreateProvider();
            await provider.InitializeInteractionModulesAsync();
            var token = Environment.GetEnvironmentVariable("AdemirAuth");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await app.RunAsync();
            await Task.Delay(-1);
        }
    }

    public static class DotEnv
    {
        public static void Load(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2)
                    continue;

                Environment.SetEnvironmentVariable(parts[0], line.Substring(line.IndexOf("=") + 1));
            }
        }
        public static void Load()
        {
            var appRoot = Directory.GetCurrentDirectory();
            var dotEnv = Path.Combine(appRoot, ".env");

            Load(dotEnv);
        }
    }
}
