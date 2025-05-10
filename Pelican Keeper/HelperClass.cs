using DSharpPlus.Entities;
using RestSharp;

namespace Pelican_Keeper;

public class HelperClass
{
    private static readonly Dictionary<string, string> LastEmbedHashes = new();
    
    internal static string FormatUptime(long uptimeMs)
    {
        var uptime = TimeSpan.FromMilliseconds(uptimeMs);
        return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
    }
    
    internal static string FormatBytes(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;
        const long tb = gb * 1024;

        return bytes switch
        {
            >= tb => $"{bytes / (double)tb:F2} TiB",
            >= gb => $"{bytes / (double)gb:F2} GiB",
            >= mb => $"{bytes / (double)mb:F2} MiB",
            >= kb => $"{bytes / (double)kb:F2} KiB",
            _ => $"{bytes} B"
        };
    }
    
    internal static RestResponse CreateRequest(RestClient client, string? token)
    {
        var request = new RestRequest("");
        request.AddHeader("Accept", "application/json");
        request.AddHeader("Authorization", "Bearer " + token);
        var response = client.Execute(request);
        return response;
    }

    internal static bool EmbedHasChanged(string uuid, DiscordEmbed newEmbed)
    {
        var hash = newEmbed.Description + string.Join(",", newEmbed.Fields.Select(f => f.Name + f.Value));
        if (LastEmbedHashes.TryGetValue(uuid, out var lastHash) && lastHash == hash) return false;
        LastEmbedHashes[uuid] = hash;
        return true;
    }

    internal static int GetEmbedCharacterCount(DiscordEmbedBuilder embed)
    {
        var count = 0;

        if (embed.Title != null)
            count += embed.Title.Length;

        if (embed.Description != null)
            count += embed.Description.Length;

        if (embed.Footer?.Text != null)
            count += embed.Footer.Text.Length;

        if (embed.Author?.Name != null)
            count += embed.Author.Name.Length;

        foreach (var field in embed.Fields)
        {
            count += field.Name?.Length ?? 0;
            count += field.Value?.Length ?? 0;
        }

        return count;
    }
}