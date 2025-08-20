using System.Text.Json;
using RestSharp;

namespace Pelican_Keeper;

using static TemplateClasses;
using static HelperClass;

public static class PelicanInterface
{
    /// <summary>
    /// Gets the server stats from the Pelican API
    /// </summary>
    /// <param name="uuid">UUID of the game server</param>
    /// <returns>The server stats response</returns>
    internal static StatsResponse? GetServerStats(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            ConsoleExt.WriteLineWithPretext("UUID is null or empty.", ConsoleExt.OutputType.Error);
            return null;
        }
        
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/client/servers/" + uuid + "/resources");
        var response = CreateRequest(client, Program.Secrets.ClientToken);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                var stats = JsonSerializer.Deserialize<StatsResponse>(response.Content, options);

                if (stats?.Attributes != null)
                    return stats;

                ConsoleExt.WriteLineWithPretext("Stats response had null attributes.");
            }
            else
            {
                ConsoleExt.WriteLineWithPretext("Empty response content.");
            }
        }
        catch (JsonException ex)
        {
            ConsoleExt.WriteLineWithPretext("JSON deserialization error: " + ex.Message);
            ConsoleExt.WriteLineWithPretext("Response content: " + response.Content);
        }

        return null;
    }

    /// <summary>
    /// Gets the list of servers from the Pelican API
    /// </summary>
    /// <returns>Server list response</returns>
    internal static ServerListResponse? GetServersList()
    {
        var client = new RestClient(Program.Secrets.ServerUrl + "/api/application/servers");
        var response = CreateRequest(client, Program.Secrets.ServerToken);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            if (response.Content != null)
            {
                var server = JsonSerializer.Deserialize<ServerListResponse>(response.Content, options);
                return server;
            }
        }
        catch (JsonException ex)
        {
            ConsoleExt.WriteLineWithPretext("JSON Error: " + ex.Message);
            ConsoleExt.WriteLineWithPretext("JSON: " + response.Content);
        }

        return null;
    }

    /// <summary>
    /// Gets alist of server stats from the Pelican API
    /// </summary>
    /// <param name="uuids">list of game server UUIDs</param>
    /// <returns>list of server stats responses</returns>
    internal static List<StatsResponse?> GetServerStatsList(List<string?> uuids)
    {
        List<StatsResponse?> stats = uuids.Select(GetServerStats).ToList();
        return stats.Where(s => s != null).ToList(); 
    }

    // This doesn't work for ARK, and most likely other games as well. I read it works for Minecraft, but I have to test it and other games.
    internal static void SendGameServerCommand(string? uuid, string command)
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
        var request = new RestRequest($"{uuid}/command", Method.Post);
        
        request.AddHeader("Authorization", $"Bearer {Program.Secrets.ClientToken}");
        request.AddHeader("Content-Type", "application/json");

        var body = new { signal = $"{command}" };
        request.AddStringBody(JsonSerializer.Serialize(body), ContentType.Json);

        var response = client.Execute(request);
        if (Program.Config.Debug)
            ConsoleExt.WriteLineWithPretext(response.Content);
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
    
    public static async Task<string?> SendGameServerCommandRcon(string ip, int port, string password, string command) //This needs to write to a list of uuids and RconService because I am going to instantiate the RconService and have it hold the information of the Rcon connection, and the list will tire the connection to the uuid.
    {
        RconService rcon = new RconService();

        await rcon.Connect(ip, port);
        bool authenticated = await rcon.AuthenticateAsync(password);
        
        if (authenticated)
        {
            if (Program.Config.Debug)
                ConsoleExt.WriteLineWithPretext("RCON connection established successfully.");

            string response = await rcon.SendCommandAsync(command);
            
            if (Program.Config.Debug)
                ConsoleExt.WriteLineWithPretext($"RCON command response: {response}");
            return response;
        }
        ConsoleExt.WriteLineWithPretext("RCON authentication failed.", ConsoleExt.OutputType.Error);
        return null;
    }

    public static async Task<string?> SendA2SRequest(string ip, int port, string command)
    {
        A2SService a2S = new A2SService();
        
        await a2S.Connect(ip, port);
        string response = await a2S.SendCommandAsync(command);
        ConsoleExt.WriteLineWithPretext(response);
        return response;
    }
}