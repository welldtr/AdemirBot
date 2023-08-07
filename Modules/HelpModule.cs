using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Enum;
using DiscordBot.Services;

namespace DiscordBot.Modules
{
    public class HelpModule : InteractionModuleBase
    {
        private readonly AudioService audioSvc;

        public HelpModule(AudioService audioSvc)
        {
            this.audioSvc = audioSvc;
        }


        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("help", "Lista os comandos do módulo")]
        public async Task Help([Summary(description: "Módulo")] HelpModuleType help)
        {
            var channel = (ITextChannel)Context.Channel;
            switch (help)
            {
                case HelpModuleType.Musica:
                    await audioSvc.Help(channel);
                    break;

                case HelpModuleType.Administrativo:
                    var admin = (await Context.Guild.GetUserAsync(Context.User.Id)).GuildPermissions.Administrator;

                    if (!admin)
                    {
                        await RespondAsync("Muito esperto você, viu 👀", ephemeral: true);
                        return;
                    }
                    await WriteHelp(channel, @"
### Comandos da Staff
- `/config-denuncias`: Configurar o Canal de Denúncias
- `/config-cargo-ademir`: Configurar cargo extra para falar com o bot
- `/macro`: Criar macros através do comando
- `/listar-macros`: Listar macros
- `/editar-macro`: Editar macros 
- `/excluir-macro`: Excluir macro
- `/massban`: Banir em massa
- `/masskick`: Expulsar em massa
- `/importlevelinfo`: Importar levels de outro bot (Lurkr)
- `/importar-historico-mensagens`: Importar histórico de mensagens
- `/usuarios-inativos`: Extrair lista de usuarios por atividade no servidor
");
                    break;

                case HelpModuleType.IA:
                    await WriteHelp(channel, @"
### Comandos para utilizar a IA (Ademir)
- `/thread`: Falar com o bot em uma thread separada
- `/restart-thread`: Reiniciar a sua thread com o Ademir
- `/dall-e`: Pedir geração de imagem ao Dall-e
- `/completar`: Gerar texto com um prompt GPT

### Dinâmica do bot de IA:
- O bot responde ao mencioná-lo (@Ademir) em qualquer chat.
- Para que o bot se lembre da conversa, lembre-se de dar reply na mensagem dele.
- Após sua segunda resposta à mensagem do bot, ele abrirá um tópico para você poder falar sem ter que dar reply.
- O bot é previamente treinado para saber algumas informações sobre o servidor, como nome, criação, dono, staff e outras informações.
");
                    break;

                case HelpModuleType.Membros:
                    await WriteHelp(channel, @"
### Comandos de membros do servidor
- `/help`: Listar os comandos por módulo
- `/rank`: Exibir card de XP
- `/leaderboard`: Visualizar ranking
- `/avatar`: Visualizar avatar
- `/banner`: Visualizar banner
- `/membercount`: Visualizar contagem dos membros
- `/membergraph`: Visualizar evolução dos membros
- `/syncrolerewards`: Sincronizar recompensas de nível
- `/denunciar`: Denunciar um usuário à staff

### Dinâmica dos ganhos de level:
- As mensagens tem ganho de XP dinâmicas seguindo a velocidade do chat.
- Os chats podem ter fatores de ganho de XP diferentes.
- A XP por call só começa a contabilizar a partir do segundo membro não bot entrar na call.
- A XP por call tem maior ganho durante um evento do servidor, stream e camera abertas.
- A XP por call tem menor ganho em caso de microfone mutado ou ensurdecido
- A XP não conta para membros mutados ou ensurdecidos pela staff
- Denunciar um membro pode render mute/kick/ban se indentificado trote, meme, ou falso
");
                    break;
            }
            await RespondAsync();
        }

        private async Task WriteHelp(ITextChannel channel, string helptext)
        {
            await channel.SendMessageAsync($" ", embed: new EmbedBuilder()
                .WithDescription(helptext).Build());
        }
    }
}
