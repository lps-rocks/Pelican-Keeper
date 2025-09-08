using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

namespace Pelican_Keeper;

using static TemplateClasses;
using static HelperClass; 
using static PelicanInterface;
using static ConsoleExt;


internal static class Program
{
    internal static DiscordChannel? TargetChannel;
    internal static Secrets Secrets = null!;
    internal static Config Config = null!;
    private static readonly EmbedBuilderService EmbedService = new();
    private static List<DiscordEmbed> _pages = null!;
    private static List<ServerInfo> _serverInfo = null!;

    private enum EmbedUpdateMode
    {
        Consolidated,
        Paginated,
        PerServer
    }


    private static async Task Main()
    {
        await FileManager.ReadSecretsFile();
        await FileManager.ReadConfigFile();

        if (FileManager.GetFilePath("MessageMarkdown.txt") == String.Empty)
        {
            Console.WriteLine("MessageMarkdown.txt not found. Creating default one.");
            _ = FileManager.CreateMessageMarkdownFile();
        }

        GetServersToMonitorFileAsync();
        ServerMarkdown.GetMarkdownFileContentAsync();

        var discord = new DiscordClient(new DiscordConfiguration
        {
            Token = Secrets.BotToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
        });

        discord.Ready += OnClientReady;
        discord.MessageDeleted += OnMessageDeleted;
        discord.ComponentInteractionCreated += OnPageFlipInteraction;
        discord.ComponentInteractionCreated += OnServerStartInteraction;
        discord.ComponentInteractionCreated += OnServerStopInteraction;
        
        await discord.ConnectAsync();
        await Task.Delay(-1);
    }

    /// <summary>
    /// Function that is called when a component interaction is created.
    /// It handles the page flipping of the paginated message using buttons.
    /// </summary>
    /// <param name="sender">DiscordClient</param>
    /// <param name="e">ComponentInteractionCreateEventArgs</param>
    /// <returns>Task of Type Task</returns>
    private static async Task<Task> OnPageFlipInteraction(DiscordClient sender, ComponentInteractionCreateEventArgs e)
    {
        if (LiveMessageStorage.GetPaginated(e.Message.Id) is not { } pagedTracked || e.User.IsBot)
        {
            if (Config.Debug)
                WriteLineWithPretext("User is Bot or is not tracked, message ID is null.", OutputType.Warning);
            return Task.CompletedTask;
        }

        int index = pagedTracked;

        switch (e.Id)
        {
            case "next_page":
                index = (index + 1) % _pages.Count;
                break;
            case "prev_page":
                index = (index - 1 + _pages.Count) % _pages.Count;
                break;
            default:
                if (Config.Debug)
                    WriteLineWithPretext("Unknown interaction ID: " + e.Id, OutputType.Warning);
                return Task.CompletedTask;
        }

        LiveMessageStorage.Save(e.Message.Id, index);
                
        if (_pages.Count == 0 || pagedTracked >= _pages.Count)
        {
            WriteLineWithPretext("No pages to show or page index out of range", OutputType.Warning);
            return Task.CompletedTask;
        }

        try
        {
            // treat "UUIDS HERE" placeholder or empty/null list as "allow all"
            bool allowAllStart = Config.AllowServerStartup == null || Config.AllowServerStartup.Length == 0 || string.Equals(Config.AllowServerStartup[0], "UUIDS HERE", StringComparison.Ordinal);
            WriteLineWithPretext("show all Start: " + allowAllStart);

            // allow only if user-startup enabled, not ignoring offline, and either allow-all or in allow-list
            bool showStart = Config is { AllowUserServerStartup: true, IgnoreOfflineServers: false, AllowServerStartup: not null } && (allowAllStart || Config.AllowServerStartup.Contains(_serverInfo[index].Uuid, StringComparer.OrdinalIgnoreCase));
            WriteLineWithPretext("show Start: " + showStart);
            
            // treat "UUIDS HERE" placeholder or empty/null list as "allow all"
            bool allowAllStop = Config.AllowServerStopping == null || Config.AllowServerStopping.Length == 0 || string.Equals(Config.AllowServerStopping[0], "UUIDS HERE", StringComparison.Ordinal);
            WriteLineWithPretext("show all Stop: " + allowAllStop);

            // allow only if user-startup enabled, not ignoring offline, and either allow-all or in stop-list
            bool showStop = Config is { AllowUserServerStopping: true, AllowServerStopping: not null } && (allowAllStop || Config.AllowServerStopping.Contains(_serverInfo[index].Uuid, StringComparer.OrdinalIgnoreCase));
            WriteLineWithPretext("show Stop: " + showStop);

            var components = new List<DiscordComponent>
            {
                new DiscordButtonComponent(ButtonStyle.Primary, "prev_page", "◀️ Previous")
            };
            if (showStop)
            {
                components.Add(new DiscordButtonComponent(ButtonStyle.Primary, $"Start: {_serverInfo[index].Uuid}", $"Start"));
            }
            if (showStop)
            {
                components.Add(new DiscordButtonComponent(ButtonStyle.Primary, $"Stop: {_serverInfo[index].Uuid}", $"Stop"));
            }
            components.Add(new DiscordButtonComponent(ButtonStyle.Primary, "next_page", "Next ▶️"));
            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(_pages[index])
                    .AddComponents(components)
            );
        }
        catch (NotFoundException nf)
        {
            if (Config.Debug)
                WriteLineWithPretext("Interaction expired or already responded to. Skipping. " + nf.Message, OutputType.Error);
        }
        catch (BadRequestException br)
        {
            if (Config.Debug)
                WriteLineWithPretext("Bad request during interaction: " + br.JsonMessage, OutputType.Error);
        }
        catch (Exception ex)
        {
            if (Config.Debug)
                WriteLineWithPretext("Unexpected error during component interaction: " + ex.Message, OutputType.Error);
        }

        return Task.CompletedTask;
    }
    
    private static async Task<Task> OnServerStartInteraction(DiscordClient sender, ComponentInteractionCreateEventArgs e)
    {
        if (e.User.IsBot)
        {
            if (Config.Debug)
                WriteLineWithPretext("User is a Bot!", OutputType.Warning);
            return Task.CompletedTask;
        }

        if (!e.Id.ToLower().Contains("start") || Config.UsersAllowedToStartServers != null && string.Equals(Config.UsersAllowedToStartServers[0], "USERID HERE", StringComparison.Ordinal) && Config.UsersAllowedToStartServers.Length != 0 && !Config.UsersAllowedToStartServers.Contains(e.User.Id.ToString()))
        {
            return Task.CompletedTask;
        }

        if (Config.Debug)
            WriteLineWithPretext("User " + e.User.Username + " clicked button with ID: " + e.Id);
        
        var id = e.Id;
        var server = _serverInfo.FirstOrDefault(s => s.Uuid == id);
        if (server == null)
        {
            if (Config.Debug)
                WriteLineWithPretext($"No server found with UUID {id}", OutputType.Warning);
            return Task.CompletedTask;
        }

        if (server.Resources?.CurrentState.ToLower() == "offline")
        {
            SendPowerCommand(server.Uuid, "start");
            if (Config.Debug)
                WriteLineWithPretext("Start command sent to server " + server.Name);
            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }
        return Task.CompletedTask;
    }
    
    private static async Task<Task> OnServerStopInteraction(DiscordClient sender, ComponentInteractionCreateEventArgs e)
    {
        if (e.User.IsBot)
        {
            if (Config.Debug)
                WriteLineWithPretext("User is a Bot!", OutputType.Warning);
            return Task.CompletedTask;
        }
        
        if (!e.Id.ToLower().Contains("stop") || Config.UsersAllowedToStopServers != null && string.Equals(Config.UsersAllowedToStopServers[0], "USERID HERE", StringComparison.Ordinal) && Config.UsersAllowedToStopServers.Length != 0 && !Config.UsersAllowedToStopServers.Contains(e.User.Id.ToString()))
        {
            return Task.CompletedTask;
        }

        if (Config.Debug)
            WriteLineWithPretext("User " + e.User.Username + " clicked button with ID: " + e.Id);
        
        var id = e.Id;
        var server = _serverInfo.FirstOrDefault(s => s.Uuid == id);
        if (server == null)
        {
            if (Config.Debug)
                WriteLineWithPretext($"No server found with UUID {id}", OutputType.Warning);
            return Task.CompletedTask;
        }

        if (server.Resources?.CurrentState.ToLower() == "online")
        {
            SendPowerCommand(server.Uuid, "stop");
            if (Config.Debug)
                WriteLineWithPretext("Stop command sent to server " + server.Name);
            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Function that is called when the bot is ready to send messages.
    /// </summary>
    /// <param name="sender">DiscordClient</param>
    /// <param name="e">ReadyEventArgs</param>
    private static async Task OnClientReady(DiscordClient sender, ReadyEventArgs e)
    {
        WriteLineWithPretext("Bot is connected and ready!");
        TargetChannel = await sender.GetChannelAsync(Secrets.ChannelId);
        WriteLineWithPretext($"Target channel: {TargetChannel.Name}");
        _ = StartStatsUpdater(sender, Secrets.ChannelId);
    }
    
    /// <summary>
    /// Function that is called when a message is deleted in the target channel.
    /// </summary>
    /// <param name="sender">DiscordClient</param>
    /// <param name="e">MessageDeleteEventArgs</param>
    /// <returns>Task</returns>
    private static Task OnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs e)
    {
        if (e.Message.Channel.Id != Secrets.ChannelId) return Task.CompletedTask;

        var liveMessageTracked = LiveMessageStorage.Get(e.Message.Id);
        if (liveMessageTracked != null)
        {
            if (Config.Debug)
                WriteLineWithPretext($"Live message {e.Message.Id} deleted in channel {e.Message.Channel.Name}. Removing from storage.");
            LiveMessageStorage.Remove(liveMessageTracked);
        }
        else if (liveMessageTracked == null)
        {
            var paginatedMessageTracked = LiveMessageStorage.GetPaginated(e.Message.Id);
            if (paginatedMessageTracked != null)
            {
                if (Config.Debug)
                    WriteLineWithPretext($"Paginated message {e.Message.Id} deleted in channel {e.Message.Channel.Name}. Removing from storage.");
                LiveMessageStorage.Remove(e.Message.Id);
            }
        }
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Starts the Sever statistics updater loop.
    /// </summary>
    /// <param name="client">Bot client</param>
    /// <param name="channelId">Target channel</param>
    private static async Task StartStatsUpdater(DiscordClient client, ulong channelId)
    {
        var channel = await client.GetChannelAsync(channelId);
        
        if (Config.MessageFormat == null)
        {
            WriteLineWithPretext("No embed mode enabled, because MessageFormat in the config is null.", OutputType.Error);
            return;
        }
        
        // When the config is set to consolidate embeds
        if (Config.MessageFormat == MessageFormat.Consolidated)
        {
            StartEmbedUpdaterLoop(
                EmbedUpdateMode.Consolidated,
                async () =>
                {
                    var serversList = GetServersList();
                    _serverInfo = serversList;
                    if (serversList.Count == 0)
                    {
                        WriteLineWithPretext("No servers found on Pelican.", OutputType.Error);
                    }
                    var uuids = serversList.Select(s => s.Uuid).ToList();
                    var embed = await EmbedService.BuildMultiServerEmbed(serversList);
                    return (uuids, embed);
                },
                async (embedObj, uuids) =>
                {
                    var embed = (DiscordEmbed)embedObj;
                    var tracked = LiveMessageStorage.Get(LiveMessageStorage.Cache?.LiveStore?.LastOrDefault());

                    if (tracked != null)
                    {
                        var msg = await channel.GetMessageAsync((ulong)tracked);
                        if (EmbedHasChanged(uuids, embed))
                        {
                            if (Config.Debug)
                                WriteLineWithPretext($"Updating message {tracked}");
                            
                            if (Config is { AllowUserServerStartup: true, IgnoreOfflineServers: false })
                            {
                                int index = 0;
                                List<string?> selectedServerUuids;
                                
                                if (Config.AllowServerStartup is { Length: > 0 } && Config.AllowServerStartup[0] != "UUIDS HERE")
                                {
                                    selectedServerUuids = uuids.Where(uuid => Config.AllowServerStartup.Contains(uuid)).ToList();
                                }
                                else
                                {
                                    selectedServerUuids = uuids;
                                }
                                
                                List<DiscordComponent> buttons = selectedServerUuids.Select(serverUuids => new DiscordButtonComponent(ButtonStyle.Primary, $"Start: {serverUuids}", $"Start: {_serverInfo[index++].Name}")).Cast<DiscordComponent>().ToList(); //TODO: add Stopping command

                                WriteLineWithPretext("Buttons created: " + buttons.Count);
                                await msg.ModifyAsync(mb =>
                                {
                                    mb.WithEmbed(embed);
                                    mb.AddRows(buttons);
                                });
                            }
                            else
                            {
                                await msg.ModifyAsync(mb =>
                                {
                                    mb.WithEmbed(embed);
                                    mb.ClearComponents();
                                });
                            }
                        }
                        else if (Config.Debug)
                            WriteLineWithPretext("Message has not changed. Skipping.");
                    }
                    else
                    {
                        if (!Config.DryRun)
                        {
                            if (Config is { AllowUserServerStartup: true, IgnoreOfflineServers: false })
                            {
                                int index = 0;
                                List<DiscordComponent> buttons = uuids.Select(serverUuids => new DiscordButtonComponent(ButtonStyle.Primary, $"Start: {serverUuids}", $"Start: {_serverInfo[index++].Name}")).Cast<DiscordComponent>().ToList();

                                WriteLineWithPretext("Buttons created: " + buttons.Count);
                                var msg = await channel.SendMessageAsync(mb =>
                                {
                                    mb.WithEmbed(embed);
                                    mb.AddRows(buttons);
                                });
                                LiveMessageStorage.Save(msg.Id);
                            }
                            else
                            {
                                var msg = await channel.SendMessageAsync(embed);
                                LiveMessageStorage.Save(msg.Id);
                            }
                        }
                    }
                },
                delaySeconds: Config.ServerUpdateInterval + Random.Shared.Next(0, Config.ServerUpdateInterval / 2)
            );
        }
        
        // When the config is set to paginate
        if (Config.MessageFormat == MessageFormat.Paginated)
        {
            StartEmbedUpdaterLoop(
                EmbedUpdateMode.Paginated,
                async () =>
                {
                    var serversList = GetServersList();
                    _serverInfo = serversList;
                    if (serversList.Count == 0)
                    {
                        WriteLineWithPretext("No servers found on Pelican.", OutputType.Error);
                    }
                    var uuids = serversList.Select(s => s.Uuid).ToList();
                    var embeds = await EmbedService.BuildPaginatedServerEmbeds(serversList);
                    return (uuids, embeds);
                },
                async (embedObj, uuids) =>
                {
                    var embeds = (List<DiscordEmbed>)embedObj;

                    if (LiveMessageStorage.Cache is { PaginatedLiveStore: not null })
                    {
                        var cacheEntry = LiveMessageStorage.Cache.PaginatedLiveStore.LastOrDefault();

                        var pagedTracked = LiveMessageStorage.GetPaginated(cacheEntry.Key);
                        if (pagedTracked != null)
                        {
                            _pages = embeds;

                            // Keeps the current page index instead of resetting to 0
                            var currentIndex = pagedTracked.Value;
                            var updatedEmbed = embeds[currentIndex];
                            
                            var msg = await channel.GetMessageAsync(cacheEntry.Key);

                            if (EmbedHasChanged(uuids, updatedEmbed))
                            {
                                if (Config.Debug)
                                    WriteLineWithPretext($"Updating paginated message {cacheEntry.Key} on page {currentIndex}");
                                await msg.ModifyAsync(updatedEmbed);
                            }
                            else if (Config.Debug)
                                WriteLineWithPretext("Message has not changed. Skipping.");
                        }
                        else
                        {
                            if (!Config.DryRun)
                            {
                                bool allowAllStart = Config.AllowServerStartup == null || Config.AllowServerStartup.Length == 0 || string.Equals(Config.AllowServerStartup[0], "UUIDS HERE", StringComparison.Ordinal);
                                bool showStart = Config is { AllowUserServerStartup: true, IgnoreOfflineServers: false, AllowServerStartup: not null } && (allowAllStart || Config.AllowServerStartup.Contains(uuids[0], StringComparer.OrdinalIgnoreCase));
                                
                                WriteLineWithPretext("show all Start: " + allowAllStart);
                                WriteLineWithPretext("show Start: " + showStart);
                                
                                bool allowAllStop = Config.AllowServerStopping == null || Config.AllowServerStopping.Length == 0 || string.Equals(Config.AllowServerStopping[0], "UUIDS HERE", StringComparison.Ordinal);
                                bool showStop = Config is { AllowUserServerStopping: true, IgnoreOfflineServers: false, AllowServerStopping: not null } && (allowAllStop || Config.AllowServerStopping.Contains(uuids[0], StringComparer.OrdinalIgnoreCase));
                                
                                WriteLineWithPretext("show all Stop: " + allowAllStop);
                                WriteLineWithPretext("show Stop: " + showStop);
                                
                                if (showStart && !showStop)
                                {
                                    string? uuid = uuids[0];
                                    var msg = await channel.SendPaginatedMessageAsync(embeds, uuid);
                                    LiveMessageStorage.Save(msg.Id, 0);
                                }
                                if (!showStart && showStop)
                                {
                                    string? uuid = uuids[0];
                                    var msg = await channel.SendPaginatedMessageAsync(embeds, null, uuid);
                                    LiveMessageStorage.Save(msg.Id, 0);
                                }
                                if (showStart && showStop)
                                {
                                    string? uuid = uuids[0];
                                    var msg = await channel.SendPaginatedMessageAsync(embeds, uuid, uuid);
                                    LiveMessageStorage.Save(msg.Id, 0);
                                }
                                else
                                {
                                    var msg = await channel.SendPaginatedMessageAsync(embeds);
                                    LiveMessageStorage.Save(msg.Id, 0);
                                }
                            }
                        }
                    }
                },
                delaySeconds: Config.ServerUpdateInterval + Random.Shared.Next(0, Config.ServerUpdateInterval / 2)
            );
        }
        
        // When the config is set to PerServerMessages
        if (Config.MessageFormat == MessageFormat.PerServer)
        {
            var serversList = GetServersList();
            _serverInfo = serversList;
            if (serversList.Count == 0)
            {
                WriteLineWithPretext("No servers found on Pelican.", OutputType.Error);
                return;
            }
            foreach (var server in serversList)
            {
                StartEmbedUpdaterLoop(
                    EmbedUpdateMode.PerServer,
                    async () =>
                    {
                        var uuid = server.Uuid;

                        var embed = await EmbedService.BuildSingleServerEmbed(server);
                        return ([uuid], embed);
                    },
                    async (embedObj, uuid) =>
                    {
                        if (embedObj is not DiscordEmbed embed) return;
                        var tracked = LiveMessageStorage.Get(LiveMessageStorage.Cache?.PaginatedLiveStore?.LastOrDefault().Key);

                        if (tracked != null)
                        {
                            var msg = await channel.GetMessageAsync((ulong)tracked);
                            if (EmbedHasChanged(uuid, embed))
                            {
                                if (Config.Debug)
                                    WriteLineWithPretext($"Updating message {tracked}");
                                
                                bool allowAll = Config.AllowServerStartup == null || Config.AllowServerStartup.Length == 0 || string.Equals(Config.AllowServerStartup[0], "UUIDS HERE", StringComparison.Ordinal);
                                bool showStart = Config is { AllowUserServerStartup: true, IgnoreOfflineServers: false, AllowServerStartup: not null } && (allowAll || Config.AllowServerStartup.Contains(uuid[0], StringComparer.OrdinalIgnoreCase));
                                
                                bool allowAllStop = Config.AllowServerStopping == null || Config.AllowServerStopping.Length == 0 || string.Equals(Config.AllowServerStopping[0], "UUIDS HERE", StringComparison.Ordinal);
                                bool showStop = Config is { AllowUserServerStopping: true, IgnoreOfflineServers: false, AllowServerStopping: not null } && (allowAllStop || Config.AllowServerStopping.Contains(uuid[0], StringComparer.OrdinalIgnoreCase));

                                await msg.ModifyAsync(mb =>
                                {
                                    mb.WithEmbed(embed);
                                    mb.ClearComponents();
                                    if (showStart)
                                        mb.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, uuid[0]!, "Start"));
                                    if (showStop)
                                        mb.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, uuid[0]!, "Stop"));
                                });
                            }
                            else if (Config.Debug)
                            {
                                WriteLineWithPretext("Message has not changed. Skipping.");
                            }
                        }
                        else
                        {
                            if (!Config.DryRun)
                            {
                                bool allowAll = Config.AllowServerStartup == null || Config.AllowServerStartup.Length == 0 || string.Equals(Config.AllowServerStartup[0], "UUIDS HERE", StringComparison.Ordinal);
                                bool showStart = Config is { AllowUserServerStartup: true, IgnoreOfflineServers: false, AllowServerStartup: not null } && (allowAll || Config.AllowServerStartup.Contains(uuid[0], StringComparer.OrdinalIgnoreCase));
                                
                                bool allowAllStop = Config.AllowServerStopping == null || Config.AllowServerStopping.Length == 0 || string.Equals(Config.AllowServerStopping[0], "UUIDS HERE", StringComparison.Ordinal);
                                bool showStop = Config is { AllowUserServerStopping: true, IgnoreOfflineServers: false, AllowServerStopping: not null } && (allowAllStop || Config.AllowServerStopping.Contains(uuid[0], StringComparer.OrdinalIgnoreCase));
                                
                                var msg = await channel.SendMessageAsync(mb =>
                                {
                                    mb.WithEmbed(embed);
                                    if (showStart)
                                        mb.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, uuid[0]!, "Start"));
                                    if (showStop)
                                        mb.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, uuid[0]!, "Stop"));
                                });
                                LiveMessageStorage.Save(msg.Id);
                            }
                        }
                    },
                    delaySeconds: Config.ServerUpdateInterval + Random.Shared.Next(0, 3) // randomized per-server delay
                );
            }
        }
    }

    /// <summary>
    /// Starts the embed updater loop called by the StartstatsUpdater method
    /// </summary>
    /// <param name="mode">The EmbedUpdateMode to use</param>
    /// <param name="generateEmbedsAsync">A function that generates the embeds</param>
    /// <param name="applyEmbedUpdateAsync">A function that applies the embed update</param>
    /// <param name="delaySeconds">Delay in seconds between updates</param>
    private static void StartEmbedUpdaterLoop(EmbedUpdateMode mode, Func<Task<(List<string?> uuids, object embedOrEmbeds)>> generateEmbedsAsync, Func<object, List<string?>, Task> applyEmbedUpdateAsync, int delaySeconds = 10)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var (uuids, embedData) = await generateEmbedsAsync();
                    await applyEmbedUpdateAsync(embedData, uuids);
                }
                catch (Exception ex)
                {
                    WriteLineWithPretext($"Updater error for mode {mode}: {ex.Message}", OutputType.Warning);
                    WriteLineWithPretext($"Stack trace: {ex.StackTrace}", OutputType.Warning);
                }

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        });
    }
    
    private static void AddRows(this DiscordMessageBuilder mb, IEnumerable<DiscordComponent> components)
    {
        foreach (var row in components.Chunk(5))
            mb.AddComponents(row);
    }

    /// <summary>
    /// Sends a paginated message
    /// </summary>
    /// <param name="channel">Target channel</param>
    /// <param name="embeds">List of embeds to paginate</param>
    /// <returns>The discord message</returns>
    private static async Task<DiscordMessage> SendPaginatedMessageAsync(this DiscordChannel channel, List<DiscordEmbed> embeds, string? uuid = null, string? uuid2 = null)
    {
        List<DiscordComponent> buttons =
        [
            new DiscordButtonComponent(ButtonStyle.Primary, "prev_page", "◀️ Previous")
        ];
        if (uuid != null)
        {
            buttons.Add(new DiscordButtonComponent(ButtonStyle.Primary,$"Start: {uuid}", "Start"));
            if (uuid2 != null)
                buttons.Add(new DiscordButtonComponent(ButtonStyle.Primary, $"Stop: {uuid2}", "Stop"));
        }
        buttons.Add(new DiscordButtonComponent(ButtonStyle.Primary, "next_page", "Next ▶️"));

        var messageBuilder = new DiscordMessageBuilder()
            .WithEmbed(embeds[0])
            .AddComponents(buttons);

        var message = await messageBuilder.SendAsync(channel);

        LiveMessageStorage.Save(message.Id, 0);

        return message;
    }

}
