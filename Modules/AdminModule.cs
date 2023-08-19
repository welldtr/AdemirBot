using Discord;
using Discord.Interactions;
using DiscordBot.Utils;
using DiscordBot.Modules.Modals;
using DiscordBot.Domain.Entities;
using AngleSharp.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DiscordBot.Services;
using MongoDB.Driver;

namespace DiscordBot.Modules
{
    public class AdminModule : InteractionModuleBase
    {
        private readonly Context db;

        public AdminModule(Context context)
        {
            db = context;
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("massban", "Banir membros em massa.")]
        public async Task MassBan()
            => await Context.Interaction.RespondWithModalAsync<MassBanModal>("mass_ban");

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("masskick", "Expulsar membros em massa.")]
        public async Task MassKick()
            => await Context.Interaction.RespondWithModalAsync<MassKickModal>("mass_kick");

        [ModalInteraction("mass_ban")]
        public async Task BanResponse(MassBanModal modal)
        {
            var memberIds = StringUtils.SplitAndParseMemberIds(modal.Membros);
            await DeferAsync();
            foreach (var id in memberIds)
            {
                await (await Context.Client.GetGuildAsync(Context.Guild.Id)).AddBanAsync(id);
            }
            await Context.Channel.SendMessageAsync($"{memberIds.Length} Usuários Banidos.");
        }

        [ModalInteraction("mass_kick")]
        public async Task KickResponse(MassKickModal modal)
        {
            var memberIds = StringUtils.SplitAndParseMemberIds(modal.Membros);
            await DeferAsync();
            foreach (var id in memberIds)
            {
                var user = await (await Context.Client.GetGuildAsync(Context.Guild.Id)).GetUserAsync(id);
                if (user != null)
                    await user.KickAsync();
            }
            await Context.Channel.SendMessageAsync($"{memberIds.Length} Usuários Expulsos.");
        }

        [RequireUserPermission(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        [SlashCommand("purge", "Remover uma certa quantidade de mensagens de um canal")]
        public async Task PurgeMessages(
            [Summary(description: "Quantidade de mensgens a excluir")] int qtd,
            [Summary("canal", "Canal a ser limpo")] IMessageChannel channel = default)
        {
            await RespondAsync();
            channel = channel ?? Context.Channel;
            IEnumerable<IMessage> messages = await channel.GetMessagesAsync(qtd).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("set-voice-event-channel", "Define canal de voz dos eventos e realoca os eventos agendados")]
        public async Task SetEventChannel(
            [Summary(description: "Canal de voz")] IVoiceChannel channel)
        {
            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (cfg == null)
            {
                await RespondAsync("Configuração de eventos invalida.", ephemeral: true);
                return;
            }
            else
            {
                cfg.EventVoiceChannelId = channel.Id;
                await db.ademirCfg.UpsertAsync(cfg, a => a.GuildId == Context.Guild.Id);
                await RespondAsync("Canal de eventos de voz salvo.", ephemeral: true);

                var events = await Context.Guild.GetEventsAsync();

                foreach (var @event in events.Where(a => a.Type == GuildScheduledEventType.Voice))
                {
                    if (@event.Status == GuildScheduledEventStatus.Scheduled)
                        await @event.ModifyAsync(a => a.ChannelId = channel.Id);
                }
            }
        }


        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("set-stage-event-channel", "Define canal de palco dos eventos e realoca os eventos agendados")]
        public async Task SetStageEventChannel(
            [Summary(description: "Canal Palco")] IStageChannel channel)
        {
            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (cfg == null)
            {
                await RespondAsync("Configuração de eventos invalida.", ephemeral: true);
                return;
            }
            else
            {
                cfg.EventStageChannelId = channel.Id;
                await db.ademirCfg.UpsertAsync(cfg, a => a.GuildId == Context.Guild.Id);
                await RespondAsync("Canal de eventos palco salvo.", ephemeral: true);

                var events = await Context.Guild.GetEventsAsync();

                foreach (var @event in events.Where(a => a.Type == GuildScheduledEventType.Stage))
                {
                    if (@event.Status == GuildScheduledEventStatus.Scheduled)
                        await @event.ModifyAsync(a => a.ChannelId = channel.Id);
                }
            }
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [MessageCommand("Criar Evento de Voz")]
        public async Task CriarEvento(IMessage msg)
        {
            var content = msg.Content;
            var data = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
            try
            {
                if (content.Matches(@"([0-9]{1,2})\/([0-9]{1,2})(?:\/([0-9]{2,4}))?"))
                {
                    var match = content.Match(@"([0-9]{1,2})\/([0-9]{1,2})(?:\/([0-9]{1,4}))?").Groups;
                    var dia = match[1];
                    var mes = match[2];
                    var ano = match[3];

                    if (ano.Success)
                    {
                        data = DateTime.ParseExact($"{dia}/{mes}/{ano}", "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        data = DateTime.ParseExact($"{dia}/{mes}", "dd/MM", CultureInfo.InvariantCulture);
                    }
                }

                if (content.Matches(@"(\d\d)(?:\:|h|H)(\d\d)"))
                {
                    var match = content.Match(@"(\d\d)(?:\:|h|H)(\d\d)").Groups;
                    var hora = TimeSpan.ParseExact($"{match[1].Value}:{match[2].Value}", @"hh\:mm", CultureInfo.InvariantCulture);
                    data = data.Date + hora;
                }
                else if (content.Matches(@"(\d\d)\s?(?:hrs|Hrs|hr|horas|Horas|H|h)\s"))
                {
                    var match = content.Match(@"(\d\d)\s?(?:hrs|Hrs|hr|horas|Horas|H|h)\s").Groups;
                    var hora = TimeSpan.ParseExact(match[1].Value, "HH", CultureInfo.InvariantCulture);
                    data = data.Date + hora;
                }
            }
            catch
            {
                ;
            }
            var nome = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).First();
            await Context.Interaction.RespondWithModalAsync($"criar_evento_msg:{msg.Id}", new EventModal
            {
                Title = $"Criar Evento",
                Nome = nome,
                DataHora = data.ToString("dd/MM/yyyy HH:mm"),
                Descricao = content,
            });
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [MessageCommand("Criar Evento Palco")]
        public async Task CriarEventoPalco(IMessage msg)
        {
            var content = msg.Content;
            var data = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
            try
            {
                if (content.Matches(@"([0-9]{1,2})\/([0-9]{1,2})(?:\/([0-9]{2,4}))?"))
                {
                    var match = content.Match(@"([0-9]{1,2})\/([0-9]{1,2})(?:\/([0-9]{1,4}))?").Groups;
                    var dia = match[1];
                    var mes = match[2];
                    var ano = match[3];

                    if (ano.Success)
                    {
                        data = DateTime.ParseExact($"{dia}/{mes}/{ano}", "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        data = DateTime.ParseExact($"{dia}/{mes}", "dd/MM", CultureInfo.InvariantCulture);
                    }
                }

                if (content.Matches(@"(\d\d)(?:\:|h|H)(\d\d)"))
                {
                    var match = content.Match(@"(\d\d)(?:\:|h|H)(\d\d)").Groups;
                    var hora = TimeSpan.ParseExact($"{match[1].Value}:{match[2].Value}", @"hh\:mm", CultureInfo.InvariantCulture);
                    data = data.Date + hora;
                }
                else if (content.Matches(@"(\d\d)\s?(?:hrs|Hrs|hr|horas|Horas|H|h)\s"))
                {
                    var match = content.Match(@"(\d\d)\s?(?:hrs|Hrs|hr|horas|Horas|H|h)\s").Groups;
                    var hora = TimeSpan.ParseExact(match[1].Value, "HH", CultureInfo.InvariantCulture);
                    data = data.Date + hora;
                }
            }
            catch
            {
                ;
            }
            var nome = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).First();
            await Context.Interaction.RespondWithModalAsync($"criar_evento_palco_msg:{msg.Id}", new EventModal
            {
                Title = $"Criar Evento",
                Nome = nome,
                DataHora = data.ToString("dd/MM/yyyy HH:mm"),
                Descricao = content,
            });
        }

        [ModalInteraction(@"criar_evento_msg:*", TreatAsRegex = true)]
        public async Task CriarEventoModal(EventModal modal)
        {
            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (cfg == null)
            {
                await RespondAsync("Configuração de eventos invalida.", ephemeral: true);
                return;
            }

            string id = ((IModalInteraction)Context.Interaction).Data.CustomId;
            var msgid = ulong.Parse(Regex.Match(id, @"criar_evento_msg:(\d+)").Groups[1].Value);
            await DeferAsync(ephemeral: true);
            var msg = await Context.Channel.GetMessageAsync(msgid);

            Image? imagem = null;

            using var ms = new MemoryStream();
            if (msg.Attachments.Count > 0 && msg.Attachments.First().Url != null)
            {

                using var client = new HttpClient();
                var info = await client.GetStreamAsync(msg.Attachments.First().Url);
                info.CopyTo(ms);
                ms.Position = 0;
                imagem = new Image(ms);
            }
            try
            {
                var channels = await Context.Guild.GetChannelsAsync();
                var data = DateTime.ParseExact(modal.DataHora, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                modal.Nome = modal.Nome.Trim();
                await Context.Guild.CreateEventAsync(
                    modal.Nome,
                    data,
                    GuildScheduledEventType.Voice,
                    GuildScheduledEventPrivacyLevel.Private,
                    modal.Descricao,
                    coverImage: imagem,
                    channelId: cfg.EventVoiceChannelId);

                await Context.Interaction.ModifyOriginalResponseAsync(a => a.Content = $"Evento de voz criado.");
            }
            catch (Exception ex)
            {

                await Context.Interaction.ModifyOriginalResponseAsync(a => a.Content = ex.ToString());
            }
        }

        [ModalInteraction(@"criar_evento_palco_msg:*", TreatAsRegex = true)]
        public async Task CriarEventoPalcoModal(EventModal modal)
        {
            var cfg = await db.ademirCfg.Find(a => a.GuildId == Context.Guild.Id).FirstOrDefaultAsync();
            if (cfg == null)
            {
                await RespondAsync("Configuração de eventos invalida.", ephemeral: true);
                return;
            }

            string id = ((IModalInteraction)Context.Interaction).Data.CustomId;
            var msgid = ulong.Parse(Regex.Match(id, @"criar_evento_palco_msg:(\d+)").Groups[1].Value);
            await DeferAsync(ephemeral: true);
            var msg = await Context.Channel.GetMessageAsync(msgid);

            Image? imagem = null;

            using var ms = new MemoryStream();
            if (msg.Attachments.Count > 0 && msg.Attachments.First().Url != null)
            {

                using var client = new HttpClient();
                var info = await client.GetStreamAsync(msg.Attachments.First().Url);
                info.CopyTo(ms);
                ms.Position = 0;
                imagem = new Image(ms);
            }
            try
            {
                var channels = await Context.Guild.GetChannelsAsync();
                var data = DateTime.ParseExact(modal.DataHora, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                modal.Nome = modal.Nome.Trim();
                await Context.Guild.CreateEventAsync(
                    modal.Nome,
                    data,
                    GuildScheduledEventType.Stage,
                    GuildScheduledEventPrivacyLevel.Private,
                    modal.Descricao,
                    coverImage: imagem,
                    channelId: cfg.EventStageChannelId);

                await Context.Interaction.ModifyOriginalResponseAsync(a => a.Content = $"Evento de palco criado.");
            }
            catch (Exception ex)
            {

                await Context.Interaction.ModifyOriginalResponseAsync(a => a.Content = ex.ToString());
            }
        }
    }
}
