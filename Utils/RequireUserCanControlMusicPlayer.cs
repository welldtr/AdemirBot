using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot.Utils
{
    public class RequireUserCanControlMusicPlayer : PreconditionAttribute
    {

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.User is SocketGuildUser gUser)
            {
                var playback = ((ITextChannel)context.Channel).GetPlayback();
                if (playback.VoiceChannel == null)
                    return PreconditionResult.FromSuccess();

                var usersConnectedWithBot = await playback.VoiceChannel.GetUsersAsync().Flatten().Where(a => !a.IsBot).ToListAsync();
                if (usersConnectedWithBot.Count == 0)
                    return PreconditionResult.FromSuccess();

                if (usersConnectedWithBot.Any(a => a.Id == gUser.Id))
                    return PreconditionResult.FromError($"Você não pode controlar o player de áudio nesse momento. Aguarde até que os outros membros terminem de usar.");
                else
                    return PreconditionResult.FromSuccess();
            }
            else
                return PreconditionResult.FromError("Comando válido apenas em servidores.");
        }
    }
}
