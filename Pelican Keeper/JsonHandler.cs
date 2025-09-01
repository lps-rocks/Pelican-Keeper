using System.Text.Json;

using static Pelican_Keeper.TemplateClasses;

namespace Pelican_Keeper;

public class JsonHandler
{
    internal static int ExtractRconPort(string json, string? variableName)
    {
        if (variableName == null || variableName.Trim() == string.Empty)
        {
            variableName = "RCON_PORT";
        }
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int rconPort = 0;
        var variablesArray = root
            .GetProperty("attributes")
            .GetProperty("relationships")
            .GetProperty("variables")
            .GetProperty("data");
        
        foreach (var alloc in variablesArray.EnumerateArray())
        {
            var attr = alloc.GetProperty("attributes");
            if (attr.GetProperty("env_variable").GetString() == variableName && int.TryParse(attr.GetProperty("server_value").GetString(), out rconPort)){}
        }

        return rconPort;
    }
    
    internal static string ExtractRconPassword(string json, string? variableName)
    {
        if (variableName == null || variableName.Trim() == string.Empty)
        {
            variableName = "RCON_PASS";
        }
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string rconPassword = string.Empty;
        var variablesArray = root
            .GetProperty("attributes")
            .GetProperty("relationships")
            .GetProperty("variables")
            .GetProperty("data");
        
        foreach (var alloc in variablesArray.EnumerateArray())
        {
            var attr = alloc.GetProperty("attributes");
            if (attr.GetProperty("env_variable").GetString() == variableName)
            {
                rconPassword = attr.GetProperty("server_value").GetString() ?? string.Empty;
            }
        }

        return rconPassword;
    }
    
    internal static int ExtractQueryPort(string json, string? variableName)
    {
        if (variableName == null || variableName.Trim() == string.Empty)
        {
            variableName = "QUERY_PORT";
        }
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        int queryPort = 0;
        var variablesArray = root
            .GetProperty("attributes")
            .GetProperty("relationships")
            .GetProperty("variables")
            .GetProperty("data");
        
        foreach (var alloc in variablesArray.EnumerateArray())
        {
            var attr = alloc.GetProperty("attributes");
            if (attr.GetProperty("env_variable").GetString() == variableName && int.TryParse(attr.GetProperty("server_value").GetString(), out queryPort)){}
        }

        return queryPort;
    }
    
    public static int ExtractMaxPlayerCount(string json, string? variableName)
    {
        if (variableName == null || variableName.Trim() == string.Empty)
        {
            variableName = "MAX_PLAYERS";
        }
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int maxPlayers = 0;
        var variablesArray = root
            .GetProperty("attributes")
            .GetProperty("relationships")
            .GetProperty("allocations")
            .GetProperty("data");

        foreach (var alloc in variablesArray.EnumerateArray())
        {
            var attr = alloc.GetProperty("attributes");
            if (attr.GetProperty("env_variable").GetString() == variableName && int.TryParse(attr.GetProperty("server_value").GetString(), out maxPlayers)){}
        }

        return maxPlayers;
    }

    internal static List<ServerAllocation> ExtractAllocations(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Extract allocations
        var allocations = new List<ServerAllocation>();
        var allocationsArray = root
            .GetProperty("attributes")
            .GetProperty("relationships")
            .GetProperty("allocations")
            .GetProperty("data");

        foreach (var alloc in allocationsArray.EnumerateArray())
        {
            var attr = alloc.GetProperty("attributes");
            var ip = attr.GetProperty("ip").GetString() ?? string.Empty;
            var port = attr.GetProperty("port").GetInt32();
            var isDefault = attr.GetProperty("is_default").GetBoolean();

            allocations.Add(new ServerAllocation {
                Ip = ip,
                Port = port,
                IsDefault = isDefault
            });
        }

        return allocations;
    }
    
    internal static List<ServerInfo> ExtractServerListInfo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var serverInfo = new List<ServerInfo>();
        var serversArray = root
            .GetProperty("data")
            .EnumerateArray();
        foreach (var server in serversArray)
        {
            // Extract id, uuid, and name
            var id = server.GetProperty("attributes").GetProperty("id").GetInt32();
            var uuid = server.GetProperty("attributes").GetProperty("uuid").GetString() ?? string.Empty;
            var name = server.GetProperty("attributes").GetProperty("name").GetString() ?? string.Empty;
            
            serverInfo.Add(new ServerInfo {
                Id = id,
                Uuid = uuid,
                Name = name
            });
        }

        return serverInfo;
    }
    
    internal static ServerResources ExtractServerResources(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var attributes = root.GetProperty("attributes");
        var resources = attributes.GetProperty("resources");
        
        var currentState = attributes.GetProperty("current_state").GetString() ?? string.Empty;
        
        var memory = resources.GetProperty("memory_bytes").GetInt64();
        var cpu = resources.GetProperty("cpu_absolute").GetDouble();
        var disk = resources.GetProperty("disk_bytes").GetInt64();
        var networkRx = resources.GetProperty("network_rx_bytes").GetInt64();
        var networkTx = resources.GetProperty("network_tx_bytes").GetInt64();
        var uptime = resources.GetProperty("uptime").GetInt64();
            
        var resourcesInfo = new ServerResources {
            CurrentState = currentState,
            MemoryBytes = memory,
            CpuAbsolute = cpu,
            DiskBytes = disk,
            NetworkRxBytes = networkRx,
            NetworkTxBytes = networkTx,
            Uptime = uptime
        };

        return resourcesInfo;
    }
}