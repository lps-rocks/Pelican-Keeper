using System.Text.Json;
using RestSharp;

namespace Pelican_Keeper;

using static TemplateClasses;
using static HelperClass;

public static class PelicanInterface
{
    private static List<ServersToMonitor>? _gameCommunicationJson;
    private static List<RconService> _rconServices = new();
    
    /// <summary>
    /// Gets the server stats from the Pelican API
    /// </summary>
    /// <param name="serverInfo">Server Info Class</param>
    /// <returns>The server stats response</returns>
    private static void GetServerStats(ServerInfo serverInfo)
    {
        if (string.IsNullOrWhiteSpace(serverInfo.Uuid))
        {
            ConsoleExt.WriteLineWithPretext("UUID is null or empty.", ConsoleExt.OutputType.Error);
            return;
        }
        
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/client/servers/" + serverInfo.Uuid + "/resources");
        var response = CreateRequest(client, Program.Secrets.ClientToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                var stats = ExtractServerResources(response.Content);
                serverInfo.Resources = stats;
                return;
            }
            
            ConsoleExt.WriteLineWithPretext("Empty Stats response content.");
        }
        catch (JsonException ex)
        {
            ConsoleExt.WriteLineWithPretext("JSON deserialization or fetching Error: " + ex.Message);
            ConsoleExt.WriteLineWithPretext("Response content: " + response.Content);
        }
    }

    private static void GetServerAllocations(ServerInfo serverInfo)
    {
        if (string.IsNullOrWhiteSpace(serverInfo.Uuid))
        {
            ConsoleExt.WriteLineWithPretext("UUID is null or empty.", ConsoleExt.OutputType.Error);
            return;
        }
        
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/client/servers/" + serverInfo.Uuid);
        var response = CreateRequest(client, Program.Secrets.ClientToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                var allocations = ExtractAllocations(response.Content);
                serverInfo.Allocations = allocations;
                if (!Program.Config.PlayerCountDisplay) return;
                MonitorServers(serverInfo, response.Content);
                return;
            }

            ConsoleExt.WriteLineWithPretext("Empty Allocations response content.");
        }
        catch (JsonException ex)
        {
            ConsoleExt.WriteLineWithPretext("JSON deserialization or fetching Error: " + ex.Message, ConsoleExt.OutputType.Error, ex);
            ConsoleExt.WriteLineWithPretext("Response content: " + response.Content);
        }
    }

    /// <summary>
    /// Gets the list of servers from the Pelican API
    /// </summary>
    /// <returns>Server list response</returns>
    internal static List<ServerInfo> GetServersList()
    {
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/application/servers");
        var response = CreateRequest(client, Program.Secrets.ServerToken);

        try
        {
            if (response.Content != null)
            {
                var servers = ExtractServerListInfo(response.Content);
                GetServerStatsList(servers);
                
                if (Program.Config.ServersToIgnore != null && Program.Config.ServersToIgnore.Length > 0 && Program.Config.ServersToIgnore[0] != "UUIDS HERE")
                {
                    servers = servers.Where(s => !Program.Config.ServersToIgnore.Contains(s.Uuid)).ToList();
                }

                if (Program.Config.IgnoreOfflineServers)
                {
                    servers = servers.Where(s => s.Resources?.CurrentState.ToLower() == "online").ToList();
                }
                
                if (Program.Config.LimitServerCount && Program.Config.MaxServerCount > 0)
                {
                    if (Program.Config.ServersToDisplay != null && Program.Config.ServersToDisplay.Length > 0 && Program.Config.ServersToDisplay[0] != "UUIDS HERE")
                    {
                        servers = servers.Where(s => Program.Config.ServersToDisplay.Contains(s.Uuid)).ToList();
                    }
                    else
                    {
                        servers = servers.Take(Program.Config.MaxServerCount).ToList();
                    }
                }
                return servers;
            }
            ConsoleExt.WriteLineWithPretext("Empty Server List response content.", ConsoleExt.OutputType.Error);
        }
        catch (JsonException ex)
        {
            ConsoleExt.WriteLineWithPretext("JSON deserialization or fetching Error: " + ex.Message, ConsoleExt.OutputType.Error, ex);
            ConsoleExt.WriteLineWithPretext("JSON: " + response.Content);
        }
        return new List<ServerInfo>();
    }

    /// <summary>
    /// Gets alist of server stats from the Pelican API
    /// </summary>
    /// <param name="servers">List of Game Server Info</param>
    /// <returns>list of server stats responses</returns>
    private static void GetServerStatsList(List<ServerInfo> servers)
    {
        foreach (var server in servers)
        {
            GetServerStats(server);
            if (!Program.Config.JoinableIpDisplay) continue;
            GetServerAllocations(server);
        }
    }

    // This doesn't work for ARK, and most likely other games as well. I read it works for Minecraft, but I have to test it and other games.
    private static string? SendGameServerCommand(string? uuid, string command)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            ConsoleExt.WriteLineWithPretext("UUID is null or empty.", ConsoleExt.OutputType.Error);
            return null;
        }
        
        if (string.IsNullOrWhiteSpace(command))
        {
            ConsoleExt.WriteLineWithPretext("Command is null or empty.", ConsoleExt.OutputType.Error);
            return null;
        }
        
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/client/servers/");
        var request = new RestRequest($"{uuid}/command", Method.Post);
        
        request.AddHeader("Authorization", $"Bearer {Program.Secrets.ClientToken}");
        request.AddHeader("Content-Type", "application/json");

        var body = new { signal = $"{command}" };
        request.AddStringBody(JsonSerializer.Serialize(body), ContentType.Json);

        var response = client.Execute(request);
        if (Program.Config.Debug)
            ConsoleExt.WriteLineWithPretext(response.Content);
        return response.Content;
    }

    public static void SendPowerCommand(string? uuid, string command)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            ConsoleExt.WriteLineWithPretext("UUID is null or empty.", ConsoleExt.OutputType.Error);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(command))
        {
            ConsoleExt.WriteLineWithPretext("Command is null or empty.", ConsoleExt.OutputType.Error);
            return;
        }
        
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/client/servers/");
        var request = new RestRequest($"{uuid}/power", Method.Post);
        
        request.AddHeader("Authorization", $"Bearer {Program.Secrets.ClientToken}");
        request.AddHeader("Content-Type", "application/json");

        var body = new { signal = $"{command}" };
        request.AddStringBody(JsonSerializer.Serialize(body), ContentType.Json);

        var response = client.Execute(request);
        if (Program.Config.Debug)
            ConsoleExt.WriteLineWithPretext(response.Content);
    }
    
    // TODO: Generalize the connection protocol calls so I don't have to have separate methods for RCON and A2S and i can just generalize it with the ISendCommand interface.
    public static async Task<string?> SendGameServerCommandRcon(string ip, int port, string password, string command)
    {
        RconService rcon = new RconService(ip, port, password);
        if (_rconServices.Any(x => x.Ip == ip && x.Port == port))
        {
            rcon = _rconServices.First(x => x.Ip == ip && x.Port == port);
            if (Program.Config.Debug)
                ConsoleExt.WriteLineWithPretext("Reusing existing RCON connection to " + ip + ":" + port);
        }
        else
        {
            if (Program.Config.Debug)
                ConsoleExt.WriteLineWithPretext("Creating new RCON connection to " + ip + ":" + port);
        }

        await rcon.Connect();
        
        string response = await rcon.SendCommandAsync(command);
        
        _rconServices.Add(rcon);
        return response;
    }

    public static async Task<string?> SendA2SRequest(string ip, int port, string command) //Do I really need to store these in a list? Probably not, because I am not keeping the connection open because A2S is stateless.
    {
        A2SService a2S = new A2SService(ip, port);
        
        await a2S.Connect();
        string response = await a2S.SendCommandAsync(command);
        
        return response;
    }

    private static void MonitorServers(ServerInfo serverInfo, string json)
    {
        if (_gameCommunicationJson == null || _gameCommunicationJson.Count == 0) return;
        
        var serverToMonitor = _gameCommunicationJson.FirstOrDefault(s => s.Uuid == serverInfo.Uuid);
        if (serverToMonitor == null) return;

        switch (serverToMonitor.Protocol)
        {
            case CommandExecutionMethod.A2S:
            {
                int queryPort = ExtractQueryPort(json, serverToMonitor.QueryPortVariable);
                if (queryPort == 0)
                {
                    ConsoleExt.WriteLineWithPretext("No Query port found for server: " + serverInfo.Name, ConsoleExt.OutputType.Warning);
                    return;
                }

                if (Program.Secrets.ExternalServerIp != null && serverToMonitor.Command != null)
                {
                    var a2SResponse = SendA2SRequest(Program.Secrets.ExternalServerIp, queryPort, serverToMonitor.Command).GetAwaiter().GetResult();
                    serverInfo.PlayerCountText = a2SResponse ?? "No response from A2S query.";
                }

                return;
            }
            case CommandExecutionMethod.Rcon:
            {
                int rconPort = ExtractRconPort(json, serverToMonitor.RconPortVariable);
                string rconPassword = ExtractRconPassword(json, serverToMonitor.RconPasswordVariable);
                
                if (rconPort == 0 || string.IsNullOrWhiteSpace(rconPassword))
                {
                    ConsoleExt.WriteLineWithPretext("No RCON port or password found for server: " + serverInfo.Name, ConsoleExt.OutputType.Warning);
                    return;
                }
                
                if (Program.Secrets.ExternalServerIp != null && serverToMonitor.Command != null)
                {
                    var rconResponse = SendGameServerCommandRcon(Program.Secrets.ExternalServerIp, rconPort, rconPassword, serverToMonitor.Command).GetAwaiter().GetResult();
                    serverInfo.PlayerCountText = rconResponse ?? "No response from RCON command.";
                }
                
                break;
            }
            case CommandExecutionMethod.PelicanApi:
            {
                if (serverToMonitor.Command != null)
                {
                    var pelicanResponse = SendGameServerCommand(serverInfo.Uuid, serverToMonitor.Command);
                    serverInfo.PlayerCountText = pelicanResponse ?? "Command sent via Pelican API.";
                }
                
                break;
            }
        }
    }

    private static int ExtractRconPort(string json, string? variableName)
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
    
    private static string ExtractRconPassword(string json, string? variableName)
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
    
    private static int ExtractQueryPort(string json, string? variableName)
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

    private static List<ServerAllocation> ExtractAllocations(string json)
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
    
    private static List<ServerInfo> ExtractServerListInfo(string json)
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
    
    private static ServerResources ExtractServerResources(string json)
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
    
    public static void GetServersToMonitorFileAsync()
    {
        Task.Run(async () =>
        {
            while (Program.Config.ContinuesServerToMonitorRead)
            {
                _gameCommunicationJson = await FileManager.ReadGameCommunicationFile();
                await Task.Delay(TimeSpan.FromSeconds(Program.Config.MarkdownUpdateInterval));
            }
        });
    }
}