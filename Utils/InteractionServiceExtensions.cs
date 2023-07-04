using Discord.Interactions;
using DiscordBot.Modules;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Discord.Net;
using Newtonsoft.Json;
using Discord;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Utils
{
    public static class InteractionServiceExtensions
    {
        public static Task InitializeInteractionModulesAsync(this IServiceProvider provider)
        {
            try
            {
                var _client = provider.GetRequiredService<DiscordShardedClient>();
                var commands = provider.GetRequiredService<Discord.Commands.CommandService>();
                
                var _log = provider.GetRequiredService<ILogger<Program>>();
                provider.ActivateAllDiscordServices();

                var _interactionService = new InteractionService(_client.Rest);

                _interactionService.SlashCommandExecuted += SlashCommandExecuted;

                _client.ShardReady += async (shard) =>
                {
                    await _client.SetGameAsync($"tudo e todos [{shard.ShardId}]", type: ActivityType.Listening);
                    _log.LogInformation($"Shard Number {shard.ShardId} is connected and ready!");

                    _client.InteractionCreated += async (x) =>
                    {
                        var ctx = new ShardedInteractionContext(_client, x);
                        var _ = await Task.Run(async () => await _interactionService.ExecuteCommandAsync(ctx, provider));
                    };

                    await _interactionService.AddModulesGloballyAsync(true,
                                await _interactionService.AddModuleAsync<AdemirConfigModule>(provider),
                                await _interactionService.AddModuleAsync<BanModule>(provider),
                                await _interactionService.AddModuleAsync<ChatGPTModule>(provider),
                                await _interactionService.AddModuleAsync<DenounceModule>(provider),
                                await _interactionService.AddModuleAsync<InactiveUsersModule>(provider),
                                await _interactionService.AddModuleAsync<MacroModule>(provider),
                                await _interactionService.AddModuleAsync<MusicModule>(provider)
                            );
                };
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }

            return Task.CompletedTask;
        }

        static async Task SlashCommandExecuted(SlashCommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
        {
            if (!arg3.IsSuccess)
            {
                switch (arg3.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        await arg2.Interaction.RespondAsync($"Unmet Precondition: {arg3.ErrorReason}", ephemeral: true);
                        break;
                    case InteractionCommandError.UnknownCommand:
                        await arg2.Interaction.RespondAsync("Unknown command", ephemeral: true);
                        break;
                    case InteractionCommandError.BadArgs:
                        await arg2.Interaction.RespondAsync("Invalid number or arguments", ephemeral: true);
                        break;
                    case InteractionCommandError.Exception:
                        await arg2.Interaction.RespondAsync($"Command exception: {arg3}", ephemeral: true);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        await arg2.Interaction.RespondAsync("Command could not be executed", ephemeral: true);
                        break;
                    default:
                        break;
                }
            }
        }

    }
}
