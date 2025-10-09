﻿using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pelican_Keeper.Query_Services;
using RestSharp;

namespace Pelican_Keeper;

using static TemplateClasses;
using static HelperClass;

public static class PelicanInterface
{
    private static List<GamesToMonitor>? _gamesToMonitor = FileManager.ReadGamesToMonitorFile().GetAwaiter().GetResult();
    private static List<EggInfo>? _eggsList;
    private static List<RconService> _rconServices = new();
    private static Dictionary<string, DateTime> _shutdownTracker = new();

    /// <summary>
    /// Gets the entire List of Eggs from the Pelican API
    /// </summary>
    private static void GetEggList()
    {
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/application/eggs");
        var response = CreateRequest(client, Program.Secrets.ClientToken);
        
        try
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                _eggsList = JsonHandler.ExtractEggInfo(response.Content);
                return;
            }
            
            ConsoleExt.WriteLineWithPretext("Empty Egg List response content.");
        }
        catch (JsonException ex)
        {
            ConsoleExt.WriteLineWithPretext("JSON deserialization or fetching Error: " + ex.Message);
            ConsoleExt.WriteLineWithPretext("Response content: " + response.Content);
        }
    }
    
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

    /// <summary>
    /// Gets the Client Server List from the Pelican API, and gets the Network Allocations, and tracks the server for player count and automatic shutdown.
    /// </summary>
    /// <param name="serverInfos">List of ServerInfo</param>
    private static void GetServerAllocations(List<ServerInfo> serverInfos)
    {
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/client/?type=admin-all");
        var response = CreateRequest(client, Program.Secrets.ClientToken);
        
        try
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                var allocations = JsonHandler.ExtractNetworkAllocations(response.Content);
                foreach (var serverInfo in serverInfos)
                {
                    serverInfo.Allocations = allocations.Where(s => s.Uuid == serverInfo.Uuid).ToList();
                }
                if (!Program.Config.PlayerCountDisplay) return;
                
                foreach (var serverInfo in serverInfos)
                {
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
                                        ConsoleExt.WriteLineWithPretext($"Server {serverInfo.Name} is not in the auto-shutdown list. Skipping shutdown check.");
                                    continue;
                                }
                                
                                if (_gamesToMonitor == null || _gamesToMonitor.Count == 0)
                                {
                                    if (Program.Config.Debug)
                                        ConsoleExt.WriteLineWithPretext("No game communication configuration found. Skipping shutdown check.", ConsoleExt.OutputType.Warning);
                                    continue;
                                }
                                int playerCount = ExtractPlayerCount(serverInfo.PlayerCountText);
                                if (Program.Config.Debug)
                                    ConsoleExt.WriteLineWithPretext($"Player count: {playerCount} for server: {serverInfo.Name}");
                                if (playerCount > 0)
                                {
                                    _shutdownTracker[serverInfo.Uuid] = DateTime.Now;
                                }
                                else
                                {
                                    TimeSpan.TryParseExact(Program.Config.EmptyServerTimeout, @"d\:hh\:mm", CultureInfo.InvariantCulture, out var timeTillShutdown);
                                    if (timeTillShutdown == TimeSpan.Zero)
                                        timeTillShutdown = TimeSpan.FromHours(1);
                                    if (DateTime.Now - _shutdownTracker[serverInfo.Uuid] >= timeTillShutdown)
                                    {
                                        SendPowerCommand(serverInfo.Uuid, "stop");
                                        ConsoleExt.WriteLineWithPretext($"Server {serverInfo.Name} has been empty for over an hour. Sending shutdown command.");
                                        _shutdownTracker.Remove(serverInfo.Uuid);
                                        if (Program.Config.Debug)
                                            ConsoleExt.WriteLineWithPretext($"Server {serverInfo.Name} is stopping and removed from shutdown tracker.");
                                    }
                                }
                            }
                        }
                    }
                    else if (isTracked)
                    {
                        _shutdownTracker.Remove(serverInfo.Uuid);
                        if (Program.Config.Debug)
                            ConsoleExt.WriteLineWithPretext($"Server {serverInfo.Name} is offline or stopping. Removed from shutdown tracker.");
                    }
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
                GetEggList();
                foreach (var serverInfo in servers)
                {
                    var foundEgg = _eggsList?.Find(x => x.Id == serverInfo.Egg.Id);
                    if (foundEgg == null) continue;
                    serverInfo.Egg.Name = foundEgg.Name;
                    if (Program.Config.Debug)
                        ConsoleExt.WriteLineWithPretext($"Egg Name found: {serverInfo.Egg.Name}");
                }
                
                _ = GetServerStatsList(servers);
                
                if (Program.Config.ServersToIgnore != null && Program.Config.ServersToIgnore.Length > 0 && Program.Config.ServersToIgnore[0] != "UUIDS HERE")
                {
                    servers = servers.Where(s => !Program.Config.ServersToIgnore.Contains(s.Uuid)).ToList();
                }

                if (Program.Config.IgnoreOfflineServers)
                {
                    servers = servers.Where(s => s.Resources?.CurrentState.ToLower() != "offline" && s.Resources?.CurrentState.ToLower() != "missing").ToList();
                }
                
                if (Program.Config.IgnoreInternalServers)
                {
                    if (Program.Config.InternalIpStructure != null)
                    {
                        string internalIpPattern = "^" + Regex.Escape(Program.Config.InternalIpStructure).Replace("\\*", "\\d+") + "$";
                        servers = servers.Where(s => s.Allocations != null && s.Allocations.Any(a => !Regex.IsMatch(a.Ip, internalIpPattern))).ToList();

                    }
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
        
        // Run them all
        await Task.WhenAll(statsTasks);
        
        GetServerAllocations(servers);
    }

    /// <summary>
    /// Sends a Power command to the specified Server.
    /// </summary>
    /// <param name="uuid">UUID of the Server</param>
    /// <param name="command">Command to send ("start", "stop", etc.)</param>
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
    
    /// <summary>
    /// Sends a RCON Server command to the Specified IP and Port
    /// </summary>
    /// <param name="ip">IP of the Server</param>
    /// <param name="port">Port of the Server</param>
    /// <param name="password">RCON Password of the Server</param>
    /// <param name="command">Game command to send</param>
    /// <returns>The response to the command that was sent</returns>
    // TODO: Generalize the connection protocol calls so I don't have to have separate methods for RCON and A2S and i can just generalize it with the ISendCommand interface.
    public static async Task<string> SendRconGameServerCommand(string ip, int port, string password, string command, string? regexPattern = null)
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
        
        string response = await rcon.SendCommandAsync(command, regexPattern);
        
        _rconServices.Add(rcon);
        return response;
    }

    /// <summary>
    /// Sends a A2S(Steam Query) request to the specified IP and Port
    /// </summary>
    /// <param name="ip">IP of the Server</param>
    /// <param name="port">Port of the Server</param>
    /// <returns>The Response to the command that was sent</returns>
    public static async Task<string> SendA2SRequest(string ip, int port)
    {
        A2SService a2S = new A2SService(ip, port);
        
        await a2S.Connect();
        string response = await a2S.SendCommandAsync();
        a2S.Dispose();
        
        return response;
    }

    public static async Task<string?> SendBedrockMinecraftRequest(string ip, int port)
    {
        BedrockMinecraftQueryService bedrockMinecraftQuery = new BedrockMinecraftQueryService(ip, port);
        
        await bedrockMinecraftQuery.Connect();
        string response = await bedrockMinecraftQuery.SendCommandAsync();
        bedrockMinecraftQuery.Dispose();

        return response;
    }
    
    public static async Task<string?> SendJavaMinecraftRequest(string ip, int port)
    {
        JavaMinecraftQueryService javaMinecraftQuery = new JavaMinecraftQueryService(ip, port);
        
        await javaMinecraftQuery.Connect();
        string response = await javaMinecraftQuery.SendCommandAsync();
        javaMinecraftQuery.Dispose();
        
        return response;
    }

    /// <summary>
    /// Monitors a specified Server and getting the Player count, Max player count, and put that into a neat text
    /// </summary>
    /// <param name="serverInfo">The ServerInfo of the specific server</param>
    /// <param name="json">Input JSON</param>
    private static void MonitorServers(ServerInfo serverInfo, string json)
    {
        if (_gamesToMonitor == null || _gamesToMonitor.Count == 0) return;
        
        var serverToMonitor = _gamesToMonitor.FirstOrDefault(s => s.Game == serverInfo.Egg.Name);
        if (serverToMonitor == null)
        {
            if (Program.Config.Debug)
                ConsoleExt.WriteLineWithPretext("No monitoring configuration found for server: " + serverInfo.Name, ConsoleExt.OutputType.Warning);
            return;
        }
        ConsoleExt.WriteLineWithPretext($"Found Game to Monitor {serverToMonitor.Game}");

        int maxPlayers = JsonHandler.ExtractMaxPlayerCount(json, serverInfo.Uuid, serverToMonitor.MaxPlayerVariable, serverToMonitor.MaxPlayer);
        
        switch (serverToMonitor.Protocol)
        {
            case CommandExecutionMethod.A2S:
            {
                int queryPort = JsonHandler.ExtractQueryPort(json, serverInfo.Uuid, serverToMonitor.QueryPortVariable);

                ConsoleExt.WriteLineWithPretext("Query port for server " + serverInfo.Name + ": " + queryPort);
                if (queryPort == 0)
                {
                    ConsoleExt.WriteLineWithPretext("No Query port found for server: " + serverInfo.Name, ConsoleExt.OutputType.Warning);
                    return;
                }

                if (Program.Secrets.ExternalServerIp != null)
                {
                    ConsoleExt.WriteLineWithPretext($"Sending A2S request to {Program.Secrets.ExternalServerIp}:{queryPort} for server {serverInfo.Name}");
                    var a2SResponse = SendA2SRequest(GetCorrectIp(serverInfo), queryPort).GetAwaiter().GetResult();
                    serverInfo.PlayerCountText = a2SResponse;
                }

                return;
            }
            case CommandExecutionMethod.Rcon:
            {
                int rconPort = JsonHandler.ExtractRconPort(json, serverInfo.Uuid, serverToMonitor.RconPortVariable);
                var rconPassword = serverToMonitor.RconPassword ?? JsonHandler.ExtractRconPassword(json, serverInfo.Uuid, serverToMonitor.RconPasswordVariable);
                
                if (rconPort == 0 || string.IsNullOrWhiteSpace(rconPassword))
                {
                    ConsoleExt.WriteLineWithPretext($"No RCON port or password found for server: {serverInfo.Name}", ConsoleExt.OutputType.Warning);
                    return;
                }
                
                if (Program.Secrets.ExternalServerIp != null && serverToMonitor.Command != null)
                {
                    var rconResponse = SendRconGameServerCommand(GetCorrectIp(serverInfo), rconPort, rconPassword, serverToMonitor.Command, _gamesToMonitor.First(s => s.Game == serverInfo.Egg.Name).PlayerCountExtractRegex).GetAwaiter().GetResult();
                    serverInfo.PlayerCountText = ServerPlayerCountDisplayCleanup(rconResponse, maxPlayers);
                }
                
                break;
            }
            case CommandExecutionMethod.MinecraftJava:
            {
                int queryPort = JsonHandler.ExtractQueryPort(json, serverInfo.Uuid, serverToMonitor.QueryPortVariable);
                
                if (Program.Secrets.ExternalServerIp != null && queryPort != 0)
                {
                    var minecraftResponse = SendJavaMinecraftRequest(GetCorrectIp(serverInfo), queryPort).GetAwaiter().GetResult();
                    if (Program.Config.Debug)
                    {
                        ConsoleExt.WriteLineWithPretext($"Sent Java Minecraft Query to Serer and Port: {Program.Secrets.ExternalServerIp}:{queryPort}");
                        ConsoleExt.WriteLineWithPretext($"Java Minecraft Response: {minecraftResponse}");
                    }
                    serverInfo.PlayerCountText = minecraftResponse;
                }
                else
                {
                    if (Program.Config.Debug)
                        ConsoleExt.WriteLineWithPretext("ExternalServerIp or Query Port is null or empty", ConsoleExt.OutputType.Error);
                }
                
                break;
            }
            case CommandExecutionMethod.MinecraftBedrock:
            {
                int queryPort = JsonHandler.ExtractQueryPort(json, serverInfo.Uuid, serverToMonitor.QueryPortVariable);
                
                if (Program.Secrets.ExternalServerIp != null && queryPort != 0)
                {
                    var minecraftResponse = SendBedrockMinecraftRequest(GetCorrectIp(serverInfo), queryPort).GetAwaiter().GetResult();
                    if (Program.Config.Debug)
                    {
                        ConsoleExt.WriteLineWithPretext($"Sent Bedrock Minecraft Query to Serer and Port: {Program.Secrets.ExternalServerIp}:{queryPort}");
                        ConsoleExt.WriteLineWithPretext($"Bedrock Minecraft Response: {minecraftResponse}");
                    }
                    serverInfo.PlayerCountText = minecraftResponse;
                }
                else
                {
                    if (Program.Config.Debug)
                        ConsoleExt.WriteLineWithPretext("ExternalServerIp or Query Port is null or empty", ConsoleExt.OutputType.Error);
                }
                
                break;
            }
        }
    }
    
    /// <summary>
    /// Runs a Task to continuously get the GamesToMonitor File if continuous reading is enabled.
    /// </summary>
    public static void GetGamesToMonitorFileAsync()
    {
        Task.Run(async () =>
        {
            while (Program.Config.ContinuesGamesToMonitorRead)
            {
                _gamesToMonitor = await FileManager.ReadGamesToMonitorFile();
                await Task.Delay(TimeSpan.FromSeconds(Program.Config.MarkdownUpdateInterval));
            }
        });
    }
}