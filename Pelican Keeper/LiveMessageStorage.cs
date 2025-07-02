using System.Text.Json;
using DSharpPlus.Entities;

namespace Pelican_Keeper;

using static TemplateClasses;
using static ConsoleExt;

public static class LiveMessageStorage
{
    private const string FilePath = "MessageHistory.json";

    internal static LiveMessageJsonStorage? Cache = new();

    public static LivePaginatedMessage? GetPaginated(ulong messageId)
        => Cache?.PaginatedLiveStore?.GetValueOrDefault(messageId);

    static LiveMessageStorage()
    {
        LoadAll();
        _ = ValidateCache();
    }

    private static void LoadAll()
    {
        if (!File.Exists(FilePath))
        {
            WriteLineWithPretext("MessageHistory.json not found. Creating default one.", OutputType.Warning);
            File.Create("MessageHistory.json").Close();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            Cache = JsonSerializer.Deserialize<LiveMessageJsonStorage>(json) ?? new LiveMessageJsonStorage();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading live message cache: {ex.Message}");
            Cache = new LiveMessageJsonStorage();
        }
    }

    public static void Save(ulong messageId)
    {
        Cache?.LiveStore?.Add(messageId);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
    
    public static void Save(ulong messageId, LivePaginatedMessage msg)
    {
        if (Cache is { PaginatedLiveStore: not null } && Cache.PaginatedLiveStore.ContainsKey(messageId))
        {
            Cache.PaginatedLiveStore[messageId] = msg;
        }
        else
        {
            Cache?.PaginatedLiveStore?.Add(messageId, msg);
        }
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
    
    public static void Remove(ulong? messageId)
    {
        if (Cache != null && messageId != null && Cache.LiveStore != null && Cache.LiveStore.Remove((ulong)messageId))
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    }
    
    private static async Task ValidateCache()
    {
        if (Cache is { LiveStore: not null })
        {
            var filtered = await Cache.LiveStore
                .ToAsyncEnumerable()
                .WhereAwait(async x => Program.TargetChannel != null && await MessageExistsAsync(Program.TargetChannel, x))
                .ToHashSetAsync();
            Cache.LiveStore = filtered;
        }

        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static async Task<bool> MessageExistsAsync(DiscordChannel channel, ulong messageId)
    {
        try
        {
            var msg = await channel.GetMessageAsync(messageId);
            return msg != null;
        }
        catch (DSharpPlus.Exceptions.NotFoundException)
        {
            // Discord returned 404 (Not Found), so the message doesn't exist
            return false;
        }
    }


    public static ulong? Get(ulong? messageId)
    {
        return Cache?.LiveStore?.FirstOrDefault(x => x == messageId);
    }
}