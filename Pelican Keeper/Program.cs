using System.Text.Json;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

namespace Pelican_Keeper;

using static TemplateClasses;
using static HelperClass; 
using static PelicanInterface;
using static ConsoleExt;


//TODO: Add a way to configure how the message should be sorted, e.g. by name, by status, etc.
//TODO: Add an optional logging feature that sends a message to the target channel if a server is shutting down, starting, or has an error.
internal static class Program
{
    public static DiscordChannel? TargetChannel;
    public static Secrets Secrets = null!;
    private static Config _config = null!;
    private static readonly EmbedBuilderService EmbedService = new();
    private static List<DiscordEmbed> _pages = null!;

    private enum EmbedUpdateMode
    {
        Consolidated,
        Paginated,
        PerServer
    }


    private static async Task Main()
    {
        string secretsPath = FileManager.GetFilePath("Secrets.json");
        string configPath = FileManager.GetFilePath("Config.json");

        if (FileManager.GetFilePath("MessageMarkdown.txt") == String.Empty)
        {
            Console.WriteLine("MessageMarkdown.txt not found. Creating default one.");
            _ = FileManager.CreateMessageMarkdownFile();
        }
        
        if (secretsPath == String.Empty)
        {
            _ = FileManager.CreateSecretsFile();
            return;
        }
        
        try
        {
            var secretsJson = await File.ReadAllTextAsync(secretsPath);
            Secrets = JsonSerializer.Deserialize<Secrets>(secretsJson)!;
        }
        catch (Exception)
        {
            WriteLineWithPretext("Failed to load secrets. Secrets not filled out. Check Secrets.json", OutputType.Error);
            return;
        }

        if (configPath == String.Empty)
        {
            _ = FileManager.CreateConfigFile();
        }
        
        var configJson = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<Config>(configJson);
        if (config == null)
        {
            WriteLineWithPretext("Failed to load config.", OutputType.Error, new NullReferenceException());
            return;
        }
        _config = config;

        var discord = new DiscordClient(new DiscordConfiguration
        {
            Token = Secrets.BotToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
        });

        discord.Ready += OnClientReady;
        discord.MessageDeleted += OnMessageDeleted;
        discord.ComponentInteractionCreated += OnComponentInteractionCreated;
        
        await discord.ConnectAsync();
        await Task.Delay(-1);
    }

    /// <summary>
    /// Function that is called when a component interaction is created.
    /// It handles the page flipping of the paginated message using buttons.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    private static async Task<Task> OnComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs e)
    {
        if (LiveMessageStorage.GetPaginated(e.Message.Id) is not { } pagedTracked || e.User.IsBot)
        {
            WriteLineWithPretext("User is Bot or is not tracked message ID is null.", OutputType.Warning);
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
            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(_pages[index])
                    .AddComponents(
                        new DiscordButtonComponent(ButtonStyle.Primary, "prev_page", "◀️ Previous"),
                        new DiscordButtonComponent(ButtonStyle.Primary, "next_page", "Next ▶️")
                    )
            );
        }
        catch (NotFoundException nf)
        {
            WriteLineWithPretext("Interaction expired or already responded to. Skipping. " + nf.Message, OutputType.Error);
        }
        catch (BadRequestException br)
        {
            WriteLineWithPretext("Bad request during interaction: " + br.JsonMessage, OutputType.Error);
        }
        catch (Exception ex)
        {
            WriteLineWithPretext("Unexpected error during component interaction: " + ex.Message, OutputType.Error);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Function that is called when the bot is ready to send messages.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
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
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    private static Task OnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs e)
    {
        if (e.Message.Channel.Id != Secrets.ChannelId) return Task.CompletedTask;

        var liveMessageTracked = LiveMessageStorage.Get(e.Message.Id);
        if (liveMessageTracked != null)
        {
            WriteLineWithPretext($"Message {e.Message.Id} deleted in channel {e.Message.Channel.Name}. Removing from storage.");
            LiveMessageStorage.Remove(liveMessageTracked);
        }
        else if (liveMessageTracked == null)
        {
            var paginatedMessageTracked = LiveMessageStorage.GetPaginated(e.Message.Id);
            if (paginatedMessageTracked != null)
            {
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
        var servers = GetServersList();

        if (servers == null || servers.Data.Length == 0)
        {
            WriteLineWithPretext("No servers found.");
            return;
        }
        
        // When the config is set to consolidate embeds
        if (_config.ConsolidateEmbeds)
        {
            StartEmbedUpdaterLoop(
                EmbedUpdateMode.Consolidated,
                async () =>
                {
                    var serversList = servers.Data.ToList();
                    var uuids = serversList.Select(s => s.Attributes.Uuid).ToList();
                    var stats = GetServerStatsList(uuids);
                    var embed = await EmbedService.BuildMultiServerEmbed(serversList, stats);
                    return (uuids, embed);
                },
                async (embedObj, uuids) =>
                {
                    var embed = (DiscordEmbed)embedObj;
                    var tracked = LiveMessageStorage.Get(LiveMessageStorage.Cache?.LiveStore?.LastOrDefault());

                    if (tracked != null)
                    {
                        WriteLineWithPretext($"Updating message {tracked}");
                        var msg = await channel.GetMessageAsync((ulong)tracked);
                        if (EmbedHasChanged(uuids, embed))
                            await msg.ModifyAsync(embed);
                    }
                    else
                    {
                        var msg = await channel.SendMessageAsync(embed);
                        LiveMessageStorage.Save(msg.Id);
                    }
                });
        }
        
        // When the config is set to paginate
        if (_config.Paginate)
        {
            StartEmbedUpdaterLoop(
                EmbedUpdateMode.Paginated,
                async () =>
                {
                    var serversList = servers.Data.ToList();
                    var uuids = serversList.Select(s => s.Attributes.Uuid).ToList();
                    var stats = GetServerStatsList(uuids);
                    var embeds = await EmbedService.BuildPaginatedServerEmbeds(serversList, stats);
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

                            WriteLineWithPretext($"Updating message {cacheEntry.Key} on page {currentIndex}");
                            var msg = await channel.GetMessageAsync(cacheEntry.Key);

                            if (EmbedHasChanged(uuids, updatedEmbed))
                            {
                                await msg.ModifyAsync(updatedEmbed);
                            }
                        }
                        else
                        {
                            var msg = await SendPaginatedMessageAsync(channel, embeds);

                            LiveMessageStorage.Save(msg.Id, 0);
                        }
                    }
                });
        }
        
        // When the config is set to neither consolidate nor paginate
        if (_config is { ConsolidateEmbeds: false, Paginate: false })
        {
            foreach (var server in servers.Data)
            {
                StartEmbedUpdaterLoop(
                    EmbedUpdateMode.PerServer,
                    async () =>
                    {
                        var uuid = server.Attributes.Uuid;
                        var stats = GetServerStats(uuid);
                        if (stats == null) return ([uuid], null!);

                        var embed = await EmbedService.BuildSingleServerEmbed(server, stats);
                        return ([uuid], embed);
                    },
                    async (embedObj, uuid) =>
                    {
                        if (embedObj is not DiscordEmbed embed) return;
                        var tracked = LiveMessageStorage.Get(LiveMessageStorage.Cache?.PaginatedLiveStore?.LastOrDefault().Key);

                        if (tracked != null)
                        {
                            WriteLineWithPretext($"Updating message {tracked}");
                            var msg = await channel.GetMessageAsync((ulong)tracked);
                            if (EmbedHasChanged(uuid, embed)) await msg.ModifyAsync(embed);
                        }
                        else
                        {
                            var msg = await channel.SendMessageAsync(embed);
                            LiveMessageStorage.Save(msg.Id);
                        }
                    },
                    delaySeconds: 10 + Random.Shared.Next(0, 3) // randomized per-server delay
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


    /// <summary>
    /// Sends a paginated message
    /// </summary>
    /// <param name="channel">Target channel</param>
    /// <param name="embeds">List of embeds to paginate</param>
    /// <returns>The discord message</returns>
    private static async Task<DiscordMessage> SendPaginatedMessageAsync(DiscordChannel channel, List<DiscordEmbed> embeds)
    {
        var buttons = new DiscordComponent[]
        {
            new DiscordButtonComponent(ButtonStyle.Primary, "prev_page", "◀️ Previous"),
            new DiscordButtonComponent(ButtonStyle.Primary, "next_page", "Next ▶️")
        };

        var messageBuilder = new DiscordMessageBuilder()
            .WithEmbed(embeds[0])
            .AddComponents(buttons);

        var message = await messageBuilder.SendAsync(channel);

        LiveMessageStorage.Save(message.Id, 0);

        return message;
    }

}
