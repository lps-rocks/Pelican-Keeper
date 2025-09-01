using System.Globalization;
using System.Text.Json;
using RestSharp;

namespace Pelican_Keeper;

using static TemplateClasses;
using static HelperClass;

public static class PelicanInterface
{
    private static List<ServersToMonitor>? _gameCommunicationJson;
    private static List<RconService> _rconServices = new();
    private static Dictionary<string, DateTime> _shutdownTracker = new();
    
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
                var stats = JsonHandler.ExtractServerResources(response.Content);
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
                var allocations = JsonHandler.ExtractAllocations(response.Content);
                serverInfo.Allocations = allocations;
                if (!Program.Config.PlayerCountDisplay) return;
                
                bool isTracked = _shutdownTracker.Any(x => x.Key == serverInfo.Uuid);
                if (serverInfo.Resources?.CurrentState.ToLower() != "offline" && serverInfo.Resources?.CurrentState.ToLower() != "stopping" && serverInfo.Resources?.CurrentState.ToLower() != "starting" && serverInfo.Resources?.CurrentState.ToLower() != "missing")
                {
                    if (!isTracked)
                    {
                        _shutdownTracker[serverInfo.Uuid] = DateTime.Now;
                        ConsoleExt.WriteLineWithPretext($"{serverInfo.Name} is tracked for shutdown: {isTracked}");
                    }
                    MonitorServers(serverInfo, response.Content);
                    
                    if (Program.Config.AutomaticShutdown)
                    {
                        if (serverInfo.PlayerCountText != "N/A" && !string.IsNullOrEmpty(serverInfo.PlayerCountText))
                        {
                            if (Program.Config.ServersToAutoShutdown != null && Program.Config.ServersToAutoShutdown[0] != "UUIDS HERE" && !Program.Config.ServersToAutoShutdown.Contains(serverInfo.Uuid))
                            {
                                if (Program.Config.Debug)
                                    ConsoleExt.WriteLineWithPretext("Server " + serverInfo.Name + " is not in the auto-shutdown list. Skipping shutdown check.");
                                return;
                            }
                            
                            if (_gameCommunicationJson == null || _gameCommunicationJson.Count == 0)
                            {
                                if (Program.Config.Debug)
                                    ConsoleExt.WriteLineWithPretext("No game communication configuration found. Skipping shutdown check.", ConsoleExt.OutputType.Warning);
                                return;
                            }
                            int playerCount = ExtractPlayerCount(serverInfo.PlayerCountText, _gameCommunicationJson.First(s => s.Uuid == serverInfo.Uuid).PlayerCountExtractRegex);
                            if (Program.Config.Debug)
                                ConsoleExt.WriteLineWithPretext("Player count: " + playerCount + " for server: " + serverInfo.Name);
                            if (playerCount > 0)
                            {
                                _shutdownTracker[serverInfo.Uuid] = DateTime.Now;
                            }
                            else
                            {
                                TimeSpan.TryParse(Program.Config.EmptyServerTimeout, out var timeTillShutdown);
                                if (timeTillShutdown == TimeSpan.Zero)
                                    timeTillShutdown = TimeSpan.FromHours(1);
                                if (DateTime.Now - _shutdownTracker[serverInfo.Uuid] >= timeTillShutdown)
                                {
                                    SendPowerCommand(serverInfo.Uuid, "stop");
                                    ConsoleExt.WriteLineWithPretext($"Server {serverInfo.Name} has been empty for over an hour. Sending shutdown command.");
                                    _shutdownTracker.Remove(serverInfo.Uuid);
                                    if (Program.Config.Debug)
                                        ConsoleExt.WriteLineWithPretext("Server " + serverInfo.Name + " is stopping and removed from shutdown tracker.");
                                }
                            }
                        }
                    }
                }
                else if (isTracked)
                {
                    _shutdownTracker.Remove(serverInfo.Uuid);
                    if (Program.Config.Debug)
                        ConsoleExt.WriteLineWithPretext("Server " + serverInfo.Name + " is offline or stopping. Removed from shutdown tracker.");
                }
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
                var servers = JsonHandler.ExtractServerListInfo(response.Content);
                _ = GetServerStatsList(servers);
                
                if (Program.Config.ServersToIgnore != null && Program.Config.ServersToIgnore.Length > 0 && Program.Config.ServersToIgnore[0] != "UUIDS HERE")
                {
                    servers = servers.Where(s => !Program.Config.ServersToIgnore.Contains(s.Uuid)).ToList();
                }

                if (Program.Config.IgnoreOfflineServers)
                {
                    servers = servers.Where(s => s.Resources?.CurrentState.ToLower() != "offline" && s.Resources?.CurrentState.ToLower() != "missing").ToList();
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
                
                return SortServers(servers, Program.Config.MessageSorting, Program.Config.MessageSortingDirection);
            }
            ConsoleExt.WriteLineWithPretext("Empty Server List response content.", ConsoleExt.OutputType.Error);
        }
        catch (JsonException ex)
        {
            ConsoleExt.WriteLineWithPretext("JSON deserialization or fetching Error: " + ex.Message, ConsoleExt.OutputType.Error, ex);
            ConsoleExt.WriteLineWithPretext("JSON: " + response.Content);
        }
        return [];
    }

    /// <summary>
    /// Gets alist of server stats from the Pelican API
    /// </summary>
    /// <param name="servers">List of Game Server Info</param>
    /// <returns>list of server stats responses</returns>
    private static async Task GetServerStatsList(List<ServerInfo> servers)
    {
        var sem = new SemaphoreSlim(5);

        // Stats tasks
        var statsTasks = servers.Select(async server =>
        {
            await sem.WaitAsync();
            try
            {
                if (Program.Config.Debug)
                    ConsoleExt.WriteLineWithPretext("Fetched stats for server: " + server.Name);
                GetServerStats(server);
            }
            finally { sem.Release(); }
        });

        // Allocations tasks
        IEnumerable<Task> allocTasks = [];
        if (Program.Config.JoinableIpDisplay)
        {
            allocTasks = servers.Select(async server =>
            {
                await sem.WaitAsync();
                try
                {
                    if (Program.Config.Debug)
                        ConsoleExt.WriteLineWithPretext("Fetched allocations for server: " + server.Name);
                    GetServerAllocations(server);
                }
                finally { sem.Release(); }
            });
        }

        // Run them all
        await Task.WhenAll(statsTasks.Concat(allocTasks));
    }
    
    private static string? SendGameServerCommand(string? uuid, string command) //TODO: I need to figure out a way to extract to the current player count and the maximum player count out of the return string where the max player count can be optional
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

    public static async Task<string?> SendA2SRequest(string ip, int port)
    {
        A2SService a2S = new A2SService(ip, port);
        
        await a2S.Connect();
        string response = await a2S.SendCommandAsync();
        
        return response;
    }

    private static void MonitorServers(ServerInfo serverInfo, string json)
    {
        if (_gameCommunicationJson == null || _gameCommunicationJson.Count == 0) return;
        
        var serverToMonitor = _gameCommunicationJson.FirstOrDefault(s => s.Uuid == serverInfo.Uuid);
        if (serverToMonitor == null)
        {
            if (Program.Config.Debug)
                ConsoleExt.WriteLineWithPretext("No monitoring configuration found for server: " + serverInfo.Name, ConsoleExt.OutputType.Warning);
            return;
        }
        
        if (serverInfo.PlayerCountText != null)
            serverInfo.PlayerCountText = PlayerCountCleanup(serverInfo.PlayerCountText, serverToMonitor.PlayerCountExtractRegex, JsonHandler.ExtractMaxPlayerCount(json, serverToMonitor.MaxPlayerVariable).ToString());
        switch (serverToMonitor.Protocol)
        {
            case CommandExecutionMethod.A2S:
            {
                int queryPort = JsonHandler.ExtractQueryPort(json, serverToMonitor.QueryPortVariable);
                ConsoleExt.WriteLineWithPretext("Query port for server " + serverInfo.Name + ": " + queryPort);
                if (queryPort == 0)
                {
                    ConsoleExt.WriteLineWithPretext("No Query port found for server: " + serverInfo.Name, ConsoleExt.OutputType.Warning);
                    return;
                }

                if (Program.Secrets.ExternalServerIp != null)
                {
                    ConsoleExt.WriteLineWithPretext("Sending A2S request to " + Program.Secrets.ExternalServerIp + ":" + queryPort + " for server " + serverInfo.Name);
                    var a2SResponse = SendA2SRequest(Program.Secrets.ExternalServerIp, queryPort).GetAwaiter().GetResult();
                    serverInfo.PlayerCountText = a2SResponse ?? "No response from A2S query.";
                }

                return;
            }
            case CommandExecutionMethod.Rcon:
            {
                int rconPort = JsonHandler.ExtractRconPort(json, serverToMonitor.RconPortVariable);
                string rconPassword = JsonHandler.ExtractRconPassword(json, serverToMonitor.RconPasswordVariable);
                
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