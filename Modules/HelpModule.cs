using Discord;
using Discord.Interactions;
using DiscordBot.Domain.Enum;
using DiscordBot.Services;
using MongoDB.Driver;

namespace DiscordBot.Modules
{
    public class HelpModule : InteractionModuleBase
    {
        private readonly AudioService audioSvc;
        private readonly Context db;

        public HelpModule(AudioService audioSvc, Context ctx)
        {
            this.audioSvc = audioSvc;
            this.db = ctx;
        }


        [RequireUserPermission(GuildPermission.Connect)]
        [SlashCommand("help", "Lista os comandos do módulo")]
        public async Task Help([Summary(description: "Módulo")] HelpModuleType help)
        {
            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();

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
                    await WriteHelp(channel, $@"
### Comandos da Staff
- `/config-cargo-ademir`: Configurar cargo extra para falar com o bot
- `/config-denuncias`: Configurar o Canal de Denúncias
- `/config-rewards`: Configurar recompensas de bump
- `/set-activetalker-role`: Define o cargo de pessoas com participação ativa no server
- `/set-event-invite-role`: Define o cargo a convidar para os eventos quando houverem
- `/set-recommendation-level`: Define o level mínimo necessário para recomendar membros
- `/set-stage-event-channel`: Define o canal de Stage dos eventos
- `/set-voice-event-channel`: Define o canal de Voz dos eventos
- `/togglerolerewards`: Habilitar/Desabilitar módulo de cargos
- `/role add-level-reward`: Configura um novo cargo de level
- `/role remove-level-reward`: Remove configuração de um cargo de level
- `/purge`: Remover uma certa quantidade de mensagens de um canal
- `/lock-server`: Impede que novos membros entrem no servidor
- `/unlock-server`: Rehabilita a entrada de novos membros no servidor 
- `/disable-kick-new-accounts`: Reabilitar entrada de contas novas (15 dias)
- `/kick-new-accounts`: Reabilitar entrada de contas novas (15 dias)
- `/ban`: Bane um membro
- `/kick`: Expulsa um membro
- `/massban`: Banir em massa
- `/masskick`: Expulsar em massa
- `/enable-audio-xp`: Habilitar XP de Audio
- `/disable-audio-xp`: Desabilitar XP de Audio
- `/enable-mention-xp`: Habilitar XP de menção de membros <@&{cfg?.ActiveTalkerRole ?? 0}>
- `/disable-mention-xp`: Desabilitar XP de menção de membros <@&{cfg?.ActiveTalkerRole ?? 0}>
- `/xp add`: Adicionar XP a um usuario
- `/xp remove`: Remover XP de um usuario
- `/xp set`: Definir a quantidade de XP de um usuario
- `/macro`: Criar macros através do comando
- `/listar-macros`: Listar macros
- `/editar-macro`: Editar macros 
- `/excluir-macro`: Excluir macro
- `/importlevelinfo`: Importar levels de outro bot (Lurkr)
- `/importar-historico-mensagens`: Importar histórico de mensagens
- `/usuarios-inativos`: Extrair lista de usuarios por atividade no servidor


### Comandos de mensagem (Menu de Mensagem > Apps)
- Denunciar: Denuncia a mensagem no canal de denúncias configurado
- Blacklist: Adicionar o que foi dito na mensagem em blacklis para ser apagado sempre que aparecer (funciona com GIFs do tenor)
- Criar Evento de Voz: Lê o conteúdo da mensagem no canal de eventos e cria um evento no Discord com a data e a hora do evento extraídos da mensagem.
- Criar Evento Palco: Lê o conteúdo da mensagem no canal de eventos e cria um evento DE PALCO no Discord com a data e a hora do evento extraídos da mensagem.
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
- O bot só responde a um membro Booster, Staff ou membro que optou por participar atiavamente no server.
- O bot responde ao mencioná-lo (@Ademir) em qualquer chat.
- Para que o bot se lembre da conversa, lembre-se de dar reply na mensagem dele.
- Após sua segunda resposta à mensagem do bot, ele abrirá um tópico para você poder falar sem ter que dar reply.
- O bot é previamente treinado para saber algumas informações sobre o servidor, como nome, criação, dono, staff e outras informações.
");
                    break;

                case HelpModuleType.Membros:
                    await WriteHelp(channel, $@"
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
- Ao bumpar o servidor, você ganha XP
- Reviver o chat multiplica muito a seu ganho de XP.
- Conversar com alguém que assinou o cargo <@&{cfg?.ActiveTalkerRole ?? 0}> multiplica seus pontos de XP.
- Conversar com membros que não aparecem há um tempo aumenta seu ganho de XP.
- A XP por call só começa a contabilizar a partir do segundo membro não bot entrar na call.
- A XP por call tem maior ganho durante um evento do servidor, stream e camera abertas.
- A XP por call tem menor ganho em caso de microfone mutado ou ensurdecido
- A XP não conta para membros mutados ou ensurdecidos pela staff ou em canal AFK
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
