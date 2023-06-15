using Discord;
using Discord.Net;
using Discord.WebSocket;
using LiteDB;
using Newtonsoft.Json;

namespace DiscordBot
{
    internal class Program
    {
        public static Task Main(string[] args) => new Program().MainAsync();
        private DiscordSocketClient _client;
        private ILiteCollection<BumpConfig> bumpCfg;
        private ILiteCollection<DenunciaConfig> denunciaCfg;

        public async Task MainAsync()
        {
            var db = new LiteDatabase(@"./Ademir.db");

            bumpCfg = db.GetCollection<BumpConfig>("bump_config");
            denunciaCfg = db.GetCollection<DenunciaConfig>("denuncia_config");

            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            _client = new DiscordSocketClient(config);

            var token = Environment.GetEnvironmentVariable("AdemirAuth");

            await _client.LoginAsync(TokenType.Bot, token);
            _client.MessageReceived += _client_MessageReceived;
            _client.SlashCommandExecuted += SlashCommandHandler;
            await _client.StartAsync();

            _client.Ready += async () =>
            {
                var globalCommand = new SlashCommandBuilder();
                globalCommand.WithName("denunciar")
                .WithDescription("Utilize esse comando para denunciar algo que fere as regras do servidor.")
                .AddOption("usuario", ApplicationCommandOptionType.User, "Usuário a ser denunciado", isRequired: true)
                .AddOption("relato", ApplicationCommandOptionType.String, "Relato da denuncia", isRequired: true)
                .AddOption("testemunha", ApplicationCommandOptionType.User, "Testemunha")
                .AddOption("print", ApplicationCommandOptionType.Attachment, "Print da conversa")
                .AddOption("anonimato", ApplicationCommandOptionType.Boolean, "Postar anonimamente");


                var globalCommand2 = new SlashCommandBuilder();
                globalCommand2.WithName("config-reward")
                .WithDescription("Configure as regras das recompensas de bump.")
                .AddOption("canal", ApplicationCommandOptionType.Channel, "Canal do Bump", isRequired: true)
                .AddOption("bot", ApplicationCommandOptionType.User, "Bot reminder", isRequired: true)
                .AddOption("conteudo", ApplicationCommandOptionType.String, "Conteudo da mensagem", isRequired: true)
                .AddOption("xp", ApplicationCommandOptionType.Number, "XP por bump", isRequired: true);

                var globalCommand3 = new SlashCommandBuilder();
                globalCommand3.WithName("config-denuncias")
                .WithDescription("Configure onde as denúncias serão postadas.")
                .AddOption("canal", ApplicationCommandOptionType.Channel, "Canal de denúncias", isRequired: true);

                try
                {
                    await _client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                    await _client.CreateGlobalApplicationCommandAsync(globalCommand2.Build());
                    await _client.CreateGlobalApplicationCommandAsync(globalCommand3.Build());
                }
                catch (HttpException exception)
                {
                    var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                    Console.WriteLine(json);
                }

                Console.WriteLine("Bot is connected!");
            };


            await Task.Delay(-1);
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            switch (command.CommandName)
            {
                case "denunciar":
                    await ProcessarDenuncia(command);
                    break;

                case "config-reward":
                    await ProcessarBumpReward(command);
                    break;

                case "config-denuncias":
                    await ProcessarConfigDenuncias(command);
                    break;
            }
        }

        private async Task ProcessarDenuncia(SocketSlashCommand command)
        {
            var config = denunciaCfg.Query().Where(a => a.GuildId == command.GuildId).FirstOrDefault();

            if (config == null)
            {
                await command.RespondAsync($"O canal de denúncias ainda não está configurado.", ephemeral: true);
                return;
            }

            await command.RespondAsync($"Não se preocupe, {command.User.Username}. Esta informação será mantida em sigilo.", ephemeral: true);

            var guildId = command.GuildId;
            var guild = _client.Guilds.First(a => a.Id == guildId);
            var canal = (IMessageChannel)guild.Channels.First(a => a.Id == config.ChannelId);

            var denunciado = (IUser)command.Data.Options.First(a => a.Name == "usuario").Value;
            var relato = command.Data.Options.First(a => a.Name == "relato");
            var testemunha = (IUser?)command.Data.Options.FirstOrDefault(a => a.Name == "testemunha")?.Value;
            var print = (IAttachment?)command.Data.Options.FirstOrDefault(a => a.Name == "print")?.Value;
            var anonimato = ((bool?)command.Data.Options.FirstOrDefault(a => a.Name == "anonimato")?.Value) ?? false;
            await canal.SendMessageAsync("", false, new EmbedBuilder()
            {
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName("Denunciado").WithIsInline(true).WithValue($"{denunciado.Mention}"),
                    new EmbedFieldBuilder().WithName("Denúncia").WithValue(relato.Value),
                },
                ImageUrl = print?.Url,
                Description = anonimato ? "" : ($"|| Denúnciado por: {command.User.Mention} " + (testemunha == null ? "" : $"Testemunha: {testemunha.Mention}") + " ||")
            }.Build());
        }

        private async Task ProcessarBumpReward(SocketSlashCommand command)
        {
            var admin = _client.Guilds.First(a => a.Id == command.GuildId).GetUser(command.User.Id).GuildPermissions.Administrator;

            if (!admin)
            {
                await command.RespondAsync("Apenas administradores podem configurar as recompensas por bump.", ephemeral: true);
                return;
            }

            var canal = (IChannel)command.Data.Options.First(a => a.Name == "canal").Value;
            var bot = (IUser)command.Data.Options.First(a => a.Name == "bot").Value;
            var conteudo = ((string)command.Data.Options.First(a => a.Name == "conteudo").Value);
            var xp = ((double)command.Data.Options.First(a => a.Name == "xp").Value);


            var config = bumpCfg.Query().Where(a => a.GuildId == command.GuildId).FirstOrDefault();

            if (config == null)
            {
                config = new BumpConfig
                {
                    GuildId = command.GuildId ?? 0,
                    BumpChannelId = canal.Id,
                    BumpBotId = bot.Id,
                    BumpMessageContent = conteudo,
                    XPPerBump = (int)xp
                };
                bumpCfg.Insert(config);
            }
            else
            {
                config.BumpChannelId = canal.Id;
                config.BumpBotId = bot.Id;
                config.BumpMessageContent = conteudo;
                config.XPPerBump = (int)xp;
                bumpCfg.Update(config);
            }
            await command.RespondAsync("Recompensas por bump configuradas.", ephemeral: true);
        }

        private async Task ProcessarConfigDenuncias(SocketSlashCommand command)
        {
            var admin = _client.Guilds.First(a => a.Id == command.GuildId)
                .GetUser(command.User.Id).GuildPermissions.Administrator;

            if (!admin)
            {
                await command.RespondAsync("Apenas administradores podem configurar o canal de denuncias.", ephemeral: true);
                return;
            }

            var canal = (IChannel)command.Data.Options.First(a => a.Name == "canal").Value;

            var config = denunciaCfg.Query().Where(a => a.GuildId == command.GuildId).FirstOrDefault();
            if (config == null)
            {
                denunciaCfg.Insert(new DenunciaConfig
                {
                    GuildId = command.GuildId ?? 0,
                    ChannelId = canal.Id
                });
            }
            else
            {
                config.ChannelId = canal.Id;
                denunciaCfg.Update(config);
            }

            await command.RespondAsync("Canal de denúncias configurado.", ephemeral: true);
        }

        private Task _client_MessageReceived(SocketMessage arg)
        {
            var guildId = ((SocketTextChannel)arg.Channel).Guild.Id;

            var config = bumpCfg.Query().Where(a => a.GuildId == guildId).FirstOrDefault();

            if(config == null)
            {
                Console.WriteLine("Configuração de recompensa de bump ausente.");
                return Task.CompletedTask;
            }

            var guild = _client.Guilds.First(a => a.Id == guildId);
            var canal = (IMessageChannel)guild.Channels.First(a => a.Id == config.BumpChannelId);

            if (arg.Channel.Id == config.BumpChannelId &&
                arg.Content.Contains(config.BumpMessageContent) &&
                arg.Author.Id == config.BumpBotId)
            {
                foreach (var mentionedUser in arg.MentionedUsers)
                {
                    mentionedUser.SendMessageAsync($"Você ganhou {config.XPPerBump}xp por bumpar o servidor {guild.Name}");
                    Console.WriteLine($"{mentionedUser.Username} ganhou {config.XPPerBump}xp.");
                }
            }
            return Task.CompletedTask;
        }
    }
}