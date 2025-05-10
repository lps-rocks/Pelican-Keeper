using System.Text.Json;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace Pelican_Keeper;

using static TemplateClasses;
using static HelperClass; 
using static PelicanInterface;

internal static class Program
{
    private static DiscordChannel? _targetChannel;
    public static Secrets? Secrets;
    private static Config? Config;

    private static async Task Main()
    {
        var secretsJson = await File.ReadAllTextAsync("Secrets.json");
        Secrets = JsonSerializer.Deserialize<Secrets>(secretsJson);
        if (Secrets == null)
        {
            Console.WriteLine("Failed to load secrets.");
            return;
        }
        
        var configJson = await File.ReadAllTextAsync("Config.json");
        Config = JsonSerializer.Deserialize<Config>(configJson);
        if (Config == null)
        {
            Console.WriteLine("Failed to load config.");
            return;
        }

        var discord = new DiscordClient(new DiscordConfiguration
        {
            Token = Secrets.BotToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
        });

        discord.Ready += OnClientReady;

        await discord.ConnectAsync();
        await Task.Delay(-1);
    }

    private static async Task OnClientReady(DiscordClient sender, ReadyEventArgs e)
    {
        Console.WriteLine("Bot is connected and ready!");
        _targetChannel = await sender.GetChannelAsync(Secrets!.ChannelId);
        Console.WriteLine($"Target channel: {_targetChannel.Name}");
        _ = StartStatsUpdater(sender, Secrets.ChannelId);
    }
    
    private static async Task StartStatsUpdater(DiscordClient client, ulong channelId)
    {
        var channel = await client.GetChannelAsync(channelId);
        var servers = GetServersList();

        if (servers == null || servers.Data.Length == 0)
        {
            Console.WriteLine("No servers found.");
            return;
        }

        if (Config!.ConsolidateEmbeds)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    List<ServerResponse> serverResponses = servers.Data.ToList();
                    var uuid = serverResponses[0].Attributes.Uuid;
                    
                    try
                    {
                        var embed = await BuildEmbed(serverResponses);
                        var tracked = LiveMessageStorage.Get(uuid);

                        if (tracked != null)
                        {
                            var msg = await channel.GetMessageAsync(tracked.MessageId);
                            if (EmbedHasChanged(uuid, embed!)) await msg.ModifyAsync(embed);
                        }
                        else
                        {
                            var msg = await channel.SendMessageAsync(embed);
                            LiveMessageStorage.Save(uuid, new TrackedMessage
                            {
                                ChannelId = channel.Id,
                                MessageId = msg.Id
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Updater error for {uuid}: {ex.Message}");
                    }
                    
                    await Task.Delay(TimeSpan.FromSeconds(10)); // delay for consolidated embeds
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }
        else
        {
            foreach (var server in servers.Data)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(0, 3000)));
                    var uuid = server.Attributes.Uuid;

                    while (true)
                    {
                        try
                        {
                            var embed = await BuildEmbed(server);
                            var tracked = LiveMessageStorage.Get(uuid);

                            if (tracked != null)
                            {
                                var msg = await channel.GetMessageAsync(tracked.MessageId);
                                if (EmbedHasChanged(uuid, embed!)) await msg.ModifyAsync(embed);
                            }
                            else
                            {
                                var msg = await channel.SendMessageAsync(embed);
                                LiveMessageStorage.Save(uuid, new TrackedMessage
                                {
                                    ChannelId = channel.Id,
                                    MessageId = msg.Id
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Updater error for {uuid}: {ex.Message}");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10)); // delay per server
                    }
                    // ReSharper disable once FunctionNeverReturns
                });
            }
        }
    }
    
    private static Task<DiscordEmbed> BuildEmbed(List<ServerResponse> servers)
    {
        List<string> uuids = servers.Select(s => s.Attributes.Uuid).ToList();
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
            string memory = stats?.Attributes?.Resources?.MemoryBytes is long mem
                ? $"{mem / (1024 * 1024):N0} MB"
                : "N/A";
            string cpu = stats?.Attributes?.Resources?.CpuAbsolute is double cpuAbs
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
                             $"⏳ **Uptime:** {uptime}";

            embed.AddField($"🎮 {name}", summary, inline: true);
            
            if (embed.Fields.Count < 25) continue;
            Console.WriteLine("reached embed limit of 25 fields");
            break; // prevent Discord embed limit
        }
        
        var charCount = GetEmbedCharacterCount(embed);
        Console.WriteLine($"Embed character count: {charCount}");

        if (charCount > 6000) Console.WriteLine("⚠️ Embed exceeds the 6000 character limit!");
        
        return Task.FromResult(embed.Build()); 
    }
    
    private static async Task<DiscordEmbed?> BuildEmbed(ServerResponse server)
    {
        var stats = GetServerStats(server.Attributes.Uuid);
        if (stats == null)
        {
            await _targetChannel!.SendMessageAsync("Failed to retrieve server stats information.");
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

        embed.AddField("Server ID", server.Attributes.Id.ToString(), true);
        embed.AddField("UUID", $"`{server.Attributes.Uuid}`");

        SafeAddField(embed, "Status", stats.Attributes.CurrentState, true);
        SafeAddField(embed, "Memory", $"{FormatBytes(stats.Attributes.Resources.MemoryBytes)}", true);
        SafeAddField(embed, "CPU", $"{stats.Attributes.Resources.CpuAbsolute:0.00}%", true);
        SafeAddField(embed, "Disk", $"{FormatBytes(stats.Attributes.Resources.DiskBytes)}", true);
        SafeAddField(embed, "Network RX", $"{FormatBytes(stats.Attributes.Resources.NetworkRxBytes)}", true);
        SafeAddField(embed, "Network TX", $"{FormatBytes(stats.Attributes.Resources.NetworkTxBytes)}", true);
        SafeAddField(embed, "Uptime", $"{FormatUptime(stats.Attributes.Resources.Uptime)}",
            true);

        var charCount = GetEmbedCharacterCount(embed);
        Console.WriteLine($"Embed character count: {charCount}");

        if (charCount > 6000) Console.WriteLine("⚠️ Embed exceeds the 6000 character limit!");

        return embed.Build();
    }
}
