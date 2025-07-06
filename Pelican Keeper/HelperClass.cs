using DSharpPlus.Entities;
using RestSharp;

namespace Pelican_Keeper;

public static class HelperClass
{
    private static readonly Dictionary<string, string> LastEmbedHashes = new();
    
    internal static RestResponse CreateRequest(RestClient client, string? token)
    {
        var request = new RestRequest("");
        request.AddHeader("Accept", "application/json");
        request.AddHeader("Authorization", "Bearer " + token);
        var response = client.Execute(request);
        return response;
    }

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
}