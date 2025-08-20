using System.Text.Json.Serialization;

namespace Pelican_Keeper;

public abstract class TemplateClasses
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageFormat
    {
        PerServer,
        Consolidated,
        Paginated
    }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageSorting
    {
        Alphabetical,
        Status,
        Uptime,
        None
    }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageSortingDirection
    {
        Ascending,
        Descending
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CommandExecutionMethod
    {
        Rcon,
        A2S
    }
    
    public class Secrets
    {
        public string? ClientToken { get; init; }
        public string? ServerToken { get; init; }
        public string? ServerUrl { get; init; }
        public string? BotToken { get; init; }
        public ulong ChannelId { get; init; }
        public string? ExternalIp { get; init; }
    }
    
    public class Config
    {
        public string? InternalIpStructure { get; init; }
        public MessageFormat? MessageFormat { get; init; }
        public MessageSorting? MessageSorting { get; init; }
        public MessageSortingDirection? MessageSortingDirection { get; init; }
        public bool IgnoreOfflineServers { get; init; }
        public string[]? ServersToIgnore { get; init; }
        public CommandExecutionMethod? CommandExecutionMethod { get; init; }
        
        public bool JoinableIpDisplay { get; init; }
        public bool PlayerCountDisplay { get; init; }
        
        public bool AutomaticShutdown { get; init; }
        public string? EmptyServerTimeout { get; init; }
        public bool AllowUserServerStartup { get; init; }
        
        public bool ContinuesMarkdownRead { get; init; }
        public int MarkdownUpdateInterval { get; init; }
        private readonly int _serverUpdateInterval;
        public int ServerUpdateInterval
        {
            get => _serverUpdateInterval;
            init => _serverUpdateInterval = Math.Max(value, 10);
        }
        
        public bool LimitServerCount { get; init; }
        public int MaxServerCount { get; init; }
        public string[]? ServersToDisplay { get; init; }
        
        public bool EnableRcon { get; init; }
        public string[]? RconServersToMonitor { get; init; }
        
        public bool Debug { get; init; }
        public bool DryRun { get; init; }
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
    
    public class ServerAttributes
    {
        public int Id { get; set; }
        public string? Uuid { get; set; }
        public string Name { get; set; }
    }

    public class StatsResponse
    {
        public string Object { get; set; }
        public StatsAttributes Attributes { get; set; }
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
    
    public class LiveMessageJsonStorage
    {
        public HashSet<ulong>? LiveStore { get; set; } = new();
        public Dictionary<ulong, int>? PaginatedLiveStore { get; set; } = new();
    }
    
    public class ServerViewModel
    {
        public string IpAndPort { get; set; } = null!;
        public string? Uuid { get; set; }
        public string ServerName { get; init; } = null!;
        public string Status { get; set; } = null!;
        public string StatusIcon { get; set; } = null!;
        public string Cpu { get; set; } = null!;
        public string Memory { get; set; } = null!;
        public string Disk { get; set; } = null!;
        public string NetworkRx { get; set; } = null!;
        public string NetworkTx { get; set; } = null!;
        public string Uptime { get; set; } = null!;
    }
    
    public class GameCommunicationJson
    {
        public CommandExecutionMethod Protocol { get; set; }
        public string RconPassword { get; set; } = null!;
        public string Command { get; set; } = null!;
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