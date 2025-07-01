using System.Text.Json.Serialization;
using DSharpPlus.Entities;

namespace Pelican_Keeper;

public abstract class TemplateClasses
{
    public class Secrets
    {
        public string? ClientToken { get; set; }
        public string? ServerToken { get; set; }
        public string? ServerUrl { get; set; }
        public string? BotToken { get; set; }
        public ulong ChannelId { get; set; }
        public string? ExternalIp { get; set; }
    }
    
    public class Config
    {
        public bool ConsolidateEmbeds { get; set; }
        public bool Paginate { get; set; }
    }

    public class ServerListResponse
    {
        public string Object { get; set; }
        public ServerResponse[] Data { get; set; }
    }

    public class ServerResponse
    {
        public string Object { get; set; }
        public ServerAttributes Attributes { get; set; }
    }

    public class StatsResponse
    {
        public string Object { get; set; }
        public StatsAttributes Attributes { get; set; }
    }

    public class ServerAttributes
    {
        public int Id { get; set; }
        public string? Uuid { get; set; }
        public string Name { get; set; }
    }

    public class StatsAttributes
    {
        [JsonPropertyName("current_state")] public string CurrentState { get; set; }

        [JsonPropertyName("is_suspended")] public bool IsSuspended { get; set; }

        public StatsResources Resources { get; set; }
    }

    public class StatsResources
    {
        [JsonPropertyName("memory_bytes")] public long MemoryBytes { get; set; }

        [JsonPropertyName("cpu_absolute")] public double CpuAbsolute { get; set; }

        [JsonPropertyName("disk_bytes")] public long DiskBytes { get; set; }

        [JsonPropertyName("network_rx_bytes")] public long NetworkRxBytes { get; set; }

        [JsonPropertyName("network_tx_bytes")] public long NetworkTxBytes { get; set; }

        public long Uptime { get; set; }
    }
    
    public class LivePaginatedMessage
    {
        public List<DiscordEmbedBuilder> Pages { get; set; } = new();
        public int CurrentPageIndex { get; set; } = 0;
    }
    
    public class LiveMessageJsonStorage
    {
        public HashSet<ulong>? LiveStore { get; set; } = new();
        public Dictionary<ulong, LivePaginatedMessage>? PaginatedLiveStore { get; set; } = new();
    }

    public enum OutputSortingDirection
    {
        Ascending,
        Descending
    }
    
    public enum OutputSortingType
    {
        None,
        Id,
        Status,
        Name
    }
}