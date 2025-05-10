using System.Text.Json;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using RestSharp;

namespace Pelican_Keeper;

using static TemplateClasses;

static class Program
{
    private static DiscordChannel? _targetChannel;
    private static Secrets? _secrets;
    
    static async Task Main()
    {
        string json = await File.ReadAllTextAsync("Secrets.json");
        _secrets = JsonSerializer.Deserialize<Secrets>(json);
        
        if (_secrets == null)
        {
            Console.WriteLine("Failed to load secrets.");
            return;
        }
        
        var discord = new DiscordClient(new DiscordConfiguration
        {
            Token = _secrets.BotToken,
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
        _targetChannel = await sender.GetChannelAsync(_secrets!.ChannelId);
        _ = StartStatsUpdater(sender, _secrets!.ChannelId);
    }

    private static async Task<DiscordEmbed?> BuildEmbed(ServerResponse server)
    {
            StatsResponse? stats = GetServerStats(server.Attributes.Uuid);
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
            SafeAddField(embed, "Uptime", $"{TimeSpan.FromMilliseconds(stats.Attributes.Resources.Uptime):hh\\:mm\\:ss}", true);
            
            int charCount = GetEmbedCharacterCount(embed);
            Console.WriteLine($"Embed character count: {charCount}");

            if (charCount > 6000)
            {
                Console.WriteLine("⚠️ Embed exceeds the 6000 character limit!");
            }

            return embed.Build();
    }

    static DiscordMessage? _statsMessage;

    private static async Task StartStatsUpdater(DiscordClient client, ulong channelId)
    {
        
        var channel = await client.GetChannelAsync(channelId);
        
        ServerListResponse? servers = GetServersList();
        
        if (servers == null || servers.Data.Length == 0)
        {
            Console.WriteLine("No servers found.");
            return;
        }
        
        foreach (var server in servers.Data)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(0, 3000)));
                string uuid = server.Attributes.Uuid;

                while (true)
                {
                    try
                    {
                        var embed = await BuildEmbed(server);
                        var tracked = LiveMessageStorage.Get(uuid);

                        if (tracked != null)
                        {
                            var msg = await channel.GetMessageAsync(tracked.MessageId);
                            if (EmbedHasChanged(uuid, embed!))
                            {
                                await msg.ModifyAsync(embed: embed);
                            }
                        }
                        else
                        {
                            var msg = await channel.SendMessageAsync(embed: embed);
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
            });
        }
    }


    private static string FormatBytes(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;
        const long tb = gb * 1024;

        return bytes switch
        {
            >= tb => $"{bytes / (double)tb:F2} TiB",
            >= gb => $"{bytes / (double)gb:F2} GiB",
            >= mb => $"{bytes / (double)mb:F2} MiB",
            >= kb => $"{bytes / (double)kb:F2} KiB",
            _ => $"{bytes} B"
        };
    }


    private static StatsResponse? GetServerStats(string uuid)
    {
        var client = new RestClient(_secrets?.ServerUrl + "/api/client/servers/" + uuid + "/resources");
        RestResponse response = CreateRequest(client, _secrets?.ClientToken);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                var stats = JsonSerializer.Deserialize<StatsResponse>(response.Content, options);

                if (stats?.Attributes != null)
                    return stats;

                Console.WriteLine("Stats response had null attributes.");
            }
            else
            {
                Console.WriteLine("Empty response content.");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine("JSON deserialization error: " + ex.Message);
            Console.WriteLine("Response content: " + response.Content);
        }

        return null;
    }

    private static ServerListResponse? GetServersList()
    {
        var client = new RestClient(_secrets?.ServerUrl + "/api/application/servers");
        RestResponse response = CreateRequest(client, _secrets?.ServerToken);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        try
        {
            if (response.Content != null)
            {
                var server = JsonSerializer.Deserialize<ServerListResponse>(response.Content, options);
                return server;
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine("JSON Error: " + ex.Message);
            Console.WriteLine("JSON: " + response.Content);
        }
        
        return null; 
    }

    private static RestResponse CreateRequest(RestClient client, string? token)
    {
        var request = new RestRequest("");
        request.AddHeader("Accept", "application/json");
        request.AddHeader("Authorization", "Bearer " + token);
        RestResponse response = client.Execute(request);
        return response;
    }

    private static readonly Dictionary<string, string> LastEmbedHashes = new();

    private static bool EmbedHasChanged(string uuid, DiscordEmbed newEmbed)
    {
        var hash = newEmbed.Description + string.Join(",", newEmbed.Fields.Select(f => f.Name + f.Value));
        if (LastEmbedHashes.TryGetValue(uuid, out var lastHash) && lastHash == hash) return false;
        LastEmbedHashes[uuid] = hash;
        return true;

    }
    
    public static int GetEmbedCharacterCount(DiscordEmbedBuilder embed)
    {
        int count = 0;

        if (embed.Title != null)
            count += embed.Title.Length;

        if (embed.Description != null)
            count += embed.Description.Length;

        if (embed.Footer?.Text != null)
            count += embed.Footer.Text.Length;

        if (embed.Author?.Name != null)
            count += embed.Author.Name.Length;

        foreach (var field in embed.Fields)
        {
            count += field.Name?.Length ?? 0;
            count += field.Value?.Length ?? 0;
        }

        return count;
    }

}

public static class LiveMessageStorage
{
    private const string FilePath = "MessageHistory.json";

    private static Dictionary<string, TrackedMessage> _cache = new();

    static LiveMessageStorage()
    {
        LoadAll();
    }

    private static void LoadAll()
    {
        if (!File.Exists(FilePath))
            return;

        try
        {
            var json = File.ReadAllText(FilePath);
            _cache = JsonSerializer.Deserialize<Dictionary<string, TrackedMessage>>(json) ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading live message cache: {ex.Message}");
            _cache = new();
        }
    }

    public static void Save(string serverId, TrackedMessage message)
    {
        _cache[serverId] = message;
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_cache, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    public static TrackedMessage? Get(string serverId)
    {
        return _cache.TryGetValue(serverId, out var msg) ? msg : null;
    }
}
