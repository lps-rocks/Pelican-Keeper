using System.Text.Json;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity; //TODO: Add Pagination

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
    private static Config? _config;

    private static async Task Main()
    {
        if (!File.Exists("Secrets.json"))
        {
            WriteLineWithPretext("Secrets.json not found. Creating default one.", OutputType.Warning);
            await using var _ = File.Create("Secrets.json");
            var defaultSecrets = new string("{\n  \"ClientToken\": \"YOUR_CLIENT_TOKEN\",\n  \"ServerToken\": \"YOUR_SERVER_TOKEN\",\n  \"ServerUrl\": \"YOUR_BASIC_SERVER_URL\",\n  \"BotToken\": \"YOUR_DISCORD_BOT_TOKEN\",\n  \"ChannelId\": \"THE_CHANNEL_ID_YOU_WANT_THE_BOT_TO_POST_IN\",\n  \"ExternalServerIP\": \"YOUR_EXTERNAL_SERVER_IP\"\n}");
            await File.WriteAllTextAsync("Secrets.json", defaultSecrets);
        }
        
        var secretsJson = await File.ReadAllTextAsync("Secrets.json");
        Secrets = JsonSerializer.Deserialize<Secrets>(secretsJson)!;
        if (Secrets.BotToken == "YOUR_DISCORD_BOT_TOKEN")
        {
            WriteLineWithPretext("Failed to load secrets. Secrets not filled out. Check Secrets.json", OutputType.Error);
            return;
        }
        
        var configJson = await File.ReadAllTextAsync("Config.json");
        _config = JsonSerializer.Deserialize<Config>(configJson);
        if (_config == null)
        {
            WriteLineWithPretext("Failed to load config.", OutputType.Error);
            return;
        }

        var discord = new DiscordClient(new DiscordConfiguration
        {
            Token = Secrets.BotToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
        });

        discord.Ready += OnClientReady;
        discord.MessageDeleted += OnMessageDeleted;
        
        await discord.ConnectAsync();
        await Task.Delay(-1);
    }

    private static async Task OnClientReady(DiscordClient sender, ReadyEventArgs e)
    {
        WriteLineWithPretext("Bot is connected and ready!");
        TargetChannel = await sender.GetChannelAsync(Secrets!.ChannelId);
        WriteLineWithPretext($"Target channel: {TargetChannel.Name}");
        _ = StartStatsUpdater(sender, Secrets.ChannelId);
    }
    
    private static Task OnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs e)
    {
        if (e.Message.Channel.Id != Secrets!.ChannelId) return Task.CompletedTask;

        var tracked = LiveMessageStorage.Get(e.Message.Id);
        if (tracked == null) return Task.CompletedTask;

        WriteLineWithPretext($"Message {e.Message.Id} deleted in channel {e.Message.Channel.Name}. Removing from storage.");
        LiveMessageStorage.Remove(tracked);
        return Task.CompletedTask;
    }
    
    private static async Task StartStatsUpdater(DiscordClient client, ulong channelId)
    {
        var channel = await client.GetChannelAsync(channelId);
        var servers = GetServersList();

        if (servers == null || servers.Data.Length == 0)
        {
            WriteLineWithPretext("No servers found.");
            return;
        }

        if (_config!.ConsolidateEmbeds)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    List<ServerResponse> serverResponses = servers.Data.ToList();
                    List<string?> uuids = serverResponses.Select(s => s.Attributes.Uuid).ToList();

                    try
                    {
                        var embed = await BuildEmbed(serverResponses);
                        var tracked = LiveMessageStorage.Get(LiveMessageStorage.Cache?.LiveStore?.LastOrDefault());

                        if (tracked != null)
                        {
                            WriteLineWithPretext("Message exists, updating...");
                            var msg = await channel.GetMessageAsync((ulong)tracked);
                            if (EmbedHasChanged(uuids, embed)) await msg.ModifyAsync(embed);
                        }
                        else
                        {
                            WriteLineWithPretext("Message does not exists, sending new one...");
                            var msg = await channel.SendMessageAsync(embed);
                            LiveMessageStorage.Save(msg.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLineWithPretext($"Updater error for {string.Join(", ", uuids.Cast<object>())}: {ex.Message}", OutputType.Warning); //TODO: Rework this to add a new entry if the last message was deleted, and be more specific whats going wrong
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10)); // delay for consolidated embeds
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }

        if (_config.Paginate)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    List<ServerResponse> serverResponses = servers.Data.ToList();
                    List<string?> uuids = serverResponses.Select(s => s.Attributes.Uuid).ToList();

                    try
                    {
                        List<DiscordEmbedBuilder> embeds = await BuildPaginatedEmbeds(serverResponses);

                        if (LiveMessageStorage.Cache is { PaginatedLiveStore: not null })
                        {
                            LivePaginatedMessage? pagedTracked = LiveMessageStorage.GetPaginated(LiveMessageStorage.Cache.PaginatedLiveStore.LastOrDefault().Key);

                            if (pagedTracked != null)
                            {
                                var msg = await channel.GetMessageAsync(LiveMessageStorage.Cache.PaginatedLiveStore.Last().Key);

                                var newEmbed = embeds[pagedTracked.CurrentPageIndex];
                                if (EmbedHasChanged(uuids, newEmbed))
                                {
                                    await msg.ModifyAsync(newEmbed.Build());
                                    pagedTracked.Pages[pagedTracked.CurrentPageIndex] = newEmbed;
                                }
                            }
                            else
                            {
                                var msg = await channel.SendMessageAsync(embeds[0].Build());

                                LiveMessageStorage.Save(msg.Id, new LivePaginatedMessage
                                {
                                    Pages = embeds,
                                    CurrentPageIndex = 0
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLineWithPretext($"Updater error for {string.Join(", ", uuids.Cast<object>())}: {ex.Message}", OutputType.Warning);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10)); // delay for consolidated embeds
                }
                // ReSharper disable once FunctionNeverReturns
            });

        }
        
        if (_config is { ConsolidateEmbeds: false, Paginate: false })
        {
            foreach (var server in servers.Data)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(0, 3000)));
                    List<string?> uuid = [server.Attributes.Uuid];

                    while (true)
                    {
                        try
                        {
                            var embed = await BuildEmbed(server);
                            var tracked = LiveMessageStorage.Get(LiveMessageStorage.Cache?.PaginatedLiveStore?.LastOrDefault().Key);

                            if (tracked != null)
                            {
                                var msg = await channel.GetMessageAsync((ulong)tracked);
                                if (EmbedHasChanged(uuid, embed!)) await msg.ModifyAsync(embed);
                            }
                            else
                            {
                                var msg = await channel.SendMessageAsync(embed);
                                LiveMessageStorage.Save(msg.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteLineWithPretext($"Updater error for {uuid}: {ex.Message}", OutputType.Warning);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10)); // delay per server
                    }
                    // ReSharper disable once FunctionNeverReturns
                });
            }
        }
    }

    static Task<List<DiscordEmbedBuilder>> BuildPaginatedEmbeds(List<ServerResponse> servers)
    {
        const int serversPerPage = 5;
        List<DiscordEmbedBuilder> pages = new();

        for (int i = 0; i < servers.Count; i += serversPerPage)
        {
            var pageServers = servers.Skip(i).Take(serversPerPage);

            var embed = new DiscordEmbedBuilder
            {
                Title = $"📄 Server Page {pages.Count + 1}",
                Color = DiscordColor.Azure
            };

            foreach (var server in pageServers)
            {
                embed.AddField($"🎮 {server.Attributes.Name}", "Summary goes here...", inline: false); // Placeholder for summary
            }

            pages.Add(embed);
        }

        return Task.FromResult(pages);
    }

    
    private static Task<DiscordEmbed> BuildEmbed(List<ServerResponse> servers)
    {
        List<string?> uuids = servers.Select(s => s.Attributes.Uuid).ToList();
        List<StatsResponse?> statsResponses = GetServerStatsList(uuids);
            
        var embed = new DiscordEmbedBuilder
        {
            Title = "📡 Game Server Status Overview",
            Color = DiscordColor.Azure
        };

        for (int i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            var stats = statsResponses.ElementAtOrDefault(i);

            if (server?.Attributes == null)
                continue;

            string name = server.Attributes.Name;
            string status = stats?.Attributes?.CurrentState ?? "Unknown";
            string statusIcon = status.ToLower() switch
            {
                "offline" => "🔴",
                "running" => "🟢",
                _ => "⚪" // Unknown or initializing
            };
            string memory = stats?.Attributes?.Resources?.MemoryBytes is { } mem
                ? $"{mem / (1024 * 1024):N0} MB"
                : "N/A";
            string cpu = stats?.Attributes?.Resources?.CpuAbsolute is { } cpuAbs
                ? $"{cpuAbs:0.00}%"
                : "N/A";
            string disk = FormatBytes(stats?.Attributes?.Resources?.DiskBytes ?? 0);
            string networkRx = FormatBytes(stats?.Attributes?.Resources?.NetworkRxBytes ?? 0);
            string networkTx = FormatBytes(stats?.Attributes?.Resources?.NetworkTxBytes ?? 0);

            string uptime = FormatUptime(stats?.Attributes?.Resources?.Uptime ?? 0);

            string summary = $"{statusIcon} **Status:** {status}\n" +
                             $"🧠 **Memory:** {memory}\n" +
                             $"🖥️ **CPU:** {cpu}\n" +
                             $"💽 **Disk:** {disk}\n" +
                             $"📥 **Network RX:** {networkRx}\n" +
                             $"📤 **Network TX:** {networkTx}\n" +
                             $"⏳ **Uptime:** {uptime}"; // Add Server IP and port if available and configured

            embed.AddField($"🎮 {name}", summary, inline: true);
            
            if (embed.Fields.Count < 25) continue;
            WriteLineWithPretext("reached embed limit of 25 fields", OutputType.Error);
            break; // prevent Discord embed limit
        }
        
        var charCount = GetEmbedCharacterCount(embed);
        WriteLineWithPretext($"Embed character count: {charCount}");

        if (charCount > 6000) WriteLineWithPretext("⚠️ Embed exceeds the 6000 character limit!", OutputType.Error);
        
        return Task.FromResult(embed.Build()); 
    }
    
    private static async Task<DiscordEmbed?> BuildEmbed(ServerResponse server)
    {
        var stats = GetServerStats(server.Attributes.Uuid);
        if (stats == null)
        {
            await TargetChannel!.SendMessageAsync("Failed to retrieve server stats information.");
            return null;
        }

        var embed = new DiscordEmbedBuilder
        {
            Title = $"🎮 Server: {server.Attributes.Name}",
            Color = DiscordColor.Azure
        };

        void SafeAddField(DiscordEmbedBuilder builder, string name, string? value, bool inline = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                builder.AddField(name, "N/A", inline);
                return;
            }

            builder.AddField(name, value, inline);
        }
        
        string statusIcon = stats.Attributes.CurrentState.ToLower() switch
        {
            "offline" => "🔴",
            "running" => "🟢",
            _ => "⚪" // Unknown or initializing
        };

        SafeAddField(embed, $"{statusIcon} **Status** ", stats.Attributes.CurrentState, true);
        SafeAddField(embed, "🧠 **Memory:** ", $"{FormatBytes(stats.Attributes.Resources.MemoryBytes)}", true);
        SafeAddField(embed, "🖥️ **CPU:** ", $"{stats.Attributes.Resources.CpuAbsolute:0.00}%", true);
        SafeAddField(embed, "💽 **Disk:** ", $"{FormatBytes(stats.Attributes.Resources.DiskBytes)}", true);
        SafeAddField(embed, "📥 **Network RX:** ", $"{FormatBytes(stats.Attributes.Resources.NetworkRxBytes)}", true);
        SafeAddField(embed, "📤 **Network TX:** ", $"{FormatBytes(stats.Attributes.Resources.NetworkTxBytes)}", true);
        SafeAddField(embed, "⏳ **Uptime:** ", $"{FormatUptime(stats.Attributes.Resources.Uptime)}",
            true);

        var charCount = GetEmbedCharacterCount(embed);
        WriteLineWithPretext($"Embed character count: {charCount}");

        if (charCount > 6000) WriteLineWithPretext("⚠️ Embed exceeds the 6000 character limit!", OutputType.Error);

        return embed.Build();
    }
}
