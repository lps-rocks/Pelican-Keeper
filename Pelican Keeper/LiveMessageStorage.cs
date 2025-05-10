using System.Text.Json;

namespace Pelican_Keeper;

using static TemplateClasses; 

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
            _cache = JsonSerializer.Deserialize<Dictionary<string, TrackedMessage>>(json) ??
                     new Dictionary<string, TrackedMessage>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading live message cache: {ex.Message}");
            _cache = new Dictionary<string, TrackedMessage>();
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
        return _cache.GetValueOrDefault(serverId);
    }
}