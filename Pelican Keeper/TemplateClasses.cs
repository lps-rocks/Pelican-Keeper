﻿using System.Text.Json.Serialization;

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
        Name,
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
        MinecraftJava,
        MinecraftBedrock,
        Rcon,
        A2S
    }
    
    public class Secrets
    {
        public string? ClientToken { get; init; }
        public string? ServerToken { get; init; }
        public string? ServerUrl { get; init; }
        public string? BotToken { get; init; }
        public ulong[]? ChannelIds { get; init; }
        public string? ExternalServerIp { get; init; }
    }
    
    public class Config
    {
        public string? InternalIpStructure { get; init; }
        public MessageFormat MessageFormat { get; init; }
        public MessageSorting MessageSorting { get; init; }
        public MessageSortingDirection MessageSortingDirection { get; init; }
        public bool IgnoreOfflineServers { get; init; }
        public bool IgnoreInternalServers { get; init; }
        public string[]? ServersToIgnore { get; init; }
        
        public bool JoinableIpDisplay { get; init; }
        public bool PlayerCountDisplay { get; init; }
        public string[]? ServersToMonitor { get; init; }
        
        public bool AutomaticShutdown { get; init; }
        public string[]? ServersToAutoShutdown { get; init; }
        public string? EmptyServerTimeout { get; init; }
        public bool AllowUserServerStartup { get; init; }
        public string[]? AllowServerStartup { get; init; }
        public string[]? UsersAllowedToStartServers { get; init; }
        public bool AllowUserServerStopping { get; init; }
        public string[]? AllowServerStopping { get; init; }
        public string[]? UsersAllowedToStopServers { get; init; }

        public bool ContinuesMarkdownRead { get; init; }
        public bool ContinuesGamesToMonitorRead { get; init; }
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
        
        public bool Debug { get; init; }
        public bool DryRun { get; init; }
    }
    
    public class ServerInfo
    {
        public int Id { get; init; }
        public string Uuid { get; init; } = null!;
        public string Name { get; init; } = null!;
        public string Description { get; init; } = null!;
        public EggInfo Egg { get; init; } = null!;
        public ServerResources? Resources { get; set; }
        public List<ServerAllocation>? Allocations { get; set; }
        public string? PlayerCountText { get; set; }
    }

    public class ServerResources
    {
        public string CurrentState { get; init; } = null!;
        public long MemoryBytes { get; init; }
        public double CpuAbsolute { get; init; }
        public long DiskBytes { get; init; }
        public long NetworkRxBytes { get; init; }
        public long NetworkTxBytes { get; init; }
        public long Uptime { get; init; }
    }
    
    public class ServerAllocation
    {
        public string Uuid { get; init; } = null!;
        public string Ip { get; init; } = null!;
        public int Port { get; init; }
        public bool IsDefault { get; init; }
    }
    
    public class LiveMessageJsonStorage
    {
        public HashSet<ulong>? LiveStore { get; set; } = new();
        public Dictionary<ulong, int>? PaginatedLiveStore { get; set; } = new();
    }
    
    public class ServerViewModel
    {
        public string PlayerCount { get; set; } = null!;
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
    
    public class GamesToMonitor
    {
        public string Game { get; init; } = null!;
        public CommandExecutionMethod Protocol { get; init; }
        public string? RconPortVariable { get; set; }
        public string? RconPasswordVariable { get; set; }
        public string? RconPassword { get; set; }
        public string? Command { get; set; }
        public string? QueryPortVariable { get; set; }
        public string? MaxPlayerVariable { get; set; }
        public string? MaxPlayer { get; set; }
        public string? PlayerCountExtractRegex { get; set; }
    }
    
    public class EggInfo
    {
        public int Id { get; init; }
        public string Name { get; set; } = null!;
    }
}
