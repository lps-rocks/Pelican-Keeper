using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using RestSharp;

namespace Pelican_Keeper;

using static TemplateClasses;

public static class HelperClass
{
    private static readonly Dictionary<string, string> LastEmbedHashes = new();
    
    /// <summary>
    /// Creates a rest request to the Pelican API
    /// </summary>
    /// <param name="client">RestClient</param>
    /// <param name="token">Pelican API token</param>
    /// <returns>The RestResponse</returns>
    internal static RestResponse CreateRequest(RestClient client, string? token)
    {
        var request = new RestRequest("");
        request.AddHeader("Accept", "application/json");
        request.AddHeader("Authorization", "Bearer " + token);
        var response = client.Execute(request);
        return response;
    }

    /// <summary>
    /// Checks if the embed has changed
    /// </summary>
    /// <param name="uuid">list of server UUIDs</param>
    /// <param name="newEmbed">new embed</param>
    /// <returns>bool whether the embed has changed</returns>
    internal static bool EmbedHasChanged(List<string?> uuid, DiscordEmbed newEmbed)
    {
        foreach (var uuidItem in uuid)
        {
            if (uuidItem == null) continue;
            var hash = newEmbed.Description + string.Join(",", newEmbed.Fields.Select(f => f.Name + f.Value));
            if (LastEmbedHashes.TryGetValue(uuidItem, out var lastHash) && lastHash == hash) return false;
            LastEmbedHashes[uuidItem] = hash;
        }
        return true;
    }

    private static ServerAllocation? GetConnectableAllocation(ServerInfo serverInfo) //TODO: I need more logic here to determine the best allocation to use and to determine the right port if the main port is not he joining port, for example in ark se its the query port
    {
        if (serverInfo.Allocations == null || serverInfo.Allocations.Count == 0)
            ConsoleExt.WriteLineWithPretext("Empty allocations for server: " + serverInfo.Name, ConsoleExt.OutputType.Warning);
        return serverInfo.Allocations?.FirstOrDefault(allocation => allocation.IsDefault) ?? serverInfo.Allocations?.FirstOrDefault();
    }
    
    public static string GetConnectableAddress(ServerInfo serverInfo)
    {
        var allocation = GetConnectableAllocation(serverInfo);
        if (allocation == null)
        {
            ConsoleExt.WriteLineWithPretext("No connectable allocation found for server: " + serverInfo.Name, ConsoleExt.OutputType.Error);
            return "No Connectable Address";
        }

        if (Program.Config.InternalIpStructure != null)
        {
            string pattern = "^" + Regex.Escape(Program.Config.InternalIpStructure).Replace("\\*", "\\d+") + "$";
            if (Program.Config.InternalIpStructure != null && Regex.Match(allocation.Ip, pattern) is { Success: true })
            {
                return $"{allocation.Ip}:{allocation.Port}";
            }
        }
        return $"{Program.Secrets.ExternalServerIp}:{allocation.Port}"; //TODO: Allow for usage of domain names in the future
    }
    
    public static List<ServerInfo> SortServers(IEnumerable<ServerInfo> servers, MessageSorting field, MessageSortingDirection direction)
    {
        return (field, direction) switch
        {
            (MessageSorting.Name, MessageSortingDirection.Ascending) => servers.OrderBy(s => s.Name).ToList(),
            (MessageSorting.Name, MessageSortingDirection.Descending) => servers.OrderByDescending(s => s.Name).ToList(),
            (MessageSorting.Status, MessageSortingDirection.Ascending) => servers.OrderBy(s => s.Resources?.CurrentState).ToList(),
            (MessageSorting.Status, MessageSortingDirection.Descending) => servers.OrderByDescending(s => s.Resources?.CurrentState).ToList(),
            (MessageSorting.Uptime, MessageSortingDirection.Ascending) => servers.OrderBy(s => s.Resources?.Uptime).ToList(),
            (MessageSorting.Uptime, MessageSortingDirection.Descending) => servers.OrderByDescending(s => s.Resources?.Uptime).ToList(),
            _ => servers.ToList()
        };
    }

    public static int ExtractPlayerCount(string serverResponse, string? regexPattern)
    {
        var noPlayer = Regex.Match(serverResponse, @"(?i)\bNo Player\b");
        if (noPlayer.Success) return 0;
        
        var playerMaxPlayer = Regex.Match(serverResponse, @"^(\d+)\/\d+$");
        if (playerMaxPlayer.Success && int.TryParse(playerMaxPlayer.Groups[1].Value, out var playerCount))
        {
            return playerCount;
        }
        
        var arkRconPlayerList = Regex.Match(serverResponse, @"(\d+)\.\s*([^,]+),\s*(.+)$", RegexOptions.Multiline);
        if (arkRconPlayerList.Success)
        {
            var index = arkRconPlayerList.Groups[1].Value;     // player list index starting from 0
            //var playerName = arkRconPlayerList.Groups[2].Value; // "FinalPlayer"
            //var playerId = arkRconPlayerList.Groups[3].Value;   // "FinalId"
            return int.Parse(index) + 1;
        }
        return 0;
    }

    public static string PlayerCountCleanup(string serverResponse, string? regexPattern, string? maxPlayers = "Unknown")
    {
        var noPlayer = Regex.Match(serverResponse, @"(?i)\bNo Player\b");
        if (noPlayer.Success) return $"0/{maxPlayers}";
        
        var playerMaxPlayer = Regex.Match(serverResponse, @"^(\d+)\/\d+$");
        if (playerMaxPlayer.Success)
        {
            return playerMaxPlayer.Value;
        }

        if (regexPattern == @"(\d+)\.\s*([^,]+),\s*(.+)$")
        {
            
        }
        var arkRconPlayerList = Regex.Match(serverResponse, @"(\d+)\.\s*([^,]+),\s*(.+)$", RegexOptions.Multiline);
        if (arkRconPlayerList.Success)
        {
            var index = arkRconPlayerList.Groups[1].Value;     // player list index starting from 0
            //var playerName = arkRconPlayerList.Groups[2].Value; // "FinalPlayer"
            //var playerId = arkRconPlayerList.Groups[3].Value;   // "FinalId"
            return $"{int.Parse(index) + 1}/{maxPlayers}"; // Max players is unknown in this case
        }
        
        // Custom User-defined regex pattern
        if (regexPattern != null)
        {
            var customMatch = Regex.Match(serverResponse, regexPattern);
            if (customMatch.Success)
            {
                return $"{customMatch}/{maxPlayers}";
            }
        }
        return serverResponse;
    }
}