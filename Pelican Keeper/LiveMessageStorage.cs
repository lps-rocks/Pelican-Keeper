using System.Text.Json;
using DSharpPlus.Entities;

namespace Pelican_Keeper;

using static TemplateClasses;
using static ConsoleExt;

public static class LiveMessageStorage
{
    private const string HistoryFilePath = "MessageHistory.json"; //Todo: Look into why the LivePaginatedMessage is not being saved or loaded properly

    internal static LiveMessageJsonStorage? Cache = new();

    public static int? GetPaginated(ulong? messageId)
    {
        if (Cache?.PaginatedLiveStore == null || Cache.PaginatedLiveStore.Count == 0 || messageId == null) return null;
        return Cache.PaginatedLiveStore?.First(x => x.Key == messageId).Value;
    }

    static LiveMessageStorage()
    {
        LoadAll();
        _ = ValidateCache();
    }

    private static void LoadAll()
    {
        if (!File.Exists(HistoryFilePath))
        {
            WriteLineWithPretext("MessageHistory.json not found. Creating default one.", OutputType.Warning);
            using var file = File.Create("MessageHistory.json");
            using var writer = new StreamWriter(file);
            writer.Write(JsonSerializer.Serialize(new LiveMessageJsonStorage()));
        }

        try
        {
            var json = File.ReadAllText(HistoryFilePath);
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
        File.WriteAllText(HistoryFilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
    
    public static void Save(ulong messageId, int currentPageIndex)
    {
        if (Cache is { PaginatedLiveStore: not null } && Cache.PaginatedLiveStore.ContainsKey(messageId))
        {
            Cache.PaginatedLiveStore[messageId] = currentPageIndex;
        }
        else
        {
            Cache?.PaginatedLiveStore?.Add(messageId, currentPageIndex);
        }
        File.WriteAllText(HistoryFilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
    
    public static void Remove(ulong? messageId)
    {
        if (Cache != null && messageId != null && Cache.LiveStore != null && Cache.LiveStore.Remove((ulong)messageId))
        {
            File.WriteAllText(HistoryFilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        if (Cache != null && messageId != null && Cache.PaginatedLiveStore != null && Cache.PaginatedLiveStore.Remove((ulong)messageId))
        {
            File.WriteAllText(HistoryFilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
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
        
        if (Cache is { PaginatedLiveStore: not null })
        {
            var filtered = await Cache.PaginatedLiveStore
                .ToAsyncEnumerable()
                .WhereAwait(async x => Program.TargetChannel != null && await MessageExistsAsync(Program.TargetChannel, x.Key)).ToDictionaryAsync(x => x.Key, x => x.Value);
            Cache.PaginatedLiveStore = filtered;
        }

        await File.WriteAllTextAsync(HistoryFilePath, JsonSerializer.Serialize(Cache, new JsonSerializerOptions
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
        if (Cache?.LiveStore == null || Cache.LiveStore.Count == 0 || messageId == null) return null;
        return Cache.LiveStore?.First(x => x == messageId);
    }
}