using DSharpPlus.Entities;

namespace Pelican_Keeper;
using static TemplateClasses;

public class EmbedBuilderService : IEmbedBuilderService
{
    public Task<DiscordEmbed> BuildSingleServerEmbed(ServerResponse server, StatsResponse stats)
    {
        var embed = new DiscordEmbedBuilder
        {
            Title = $"🎮 Server: {server.Attributes.Name}",
            Color = DiscordColor.Azure
        };

        AddStatFields(embed, stats);

        ConsoleExt.WriteLineWithPretext($"Embed character count: {EmbedBuilderHelper.GetEmbedCharacterCount(embed)}");
        return Task.FromResult(embed.Build());
    }

    public Task<DiscordEmbed> BuildMultiServerEmbed(List<ServerResponse> servers, List<StatsResponse?> statsList)
    {
        var embed = new DiscordEmbedBuilder
        {
            Title = "📡 Game Server Status Overview",
            Color = DiscordColor.Azure
        };

        for (int i = 0; i < servers.Count && embed.Fields.Count < 25; i++)
        {
            var server = servers[i];
            var stats = statsList.ElementAtOrDefault(i);
            if (stats?.Attributes == null) continue;

            var summary = FormatSummary(stats);
            embed.AddField($"🎮 {server.Attributes.Name}", summary, inline: true);
        }

        ConsoleExt.WriteLineWithPretext($"Embed character count: {EmbedBuilderHelper.GetEmbedCharacterCount(embed)}");
        return Task.FromResult(embed.Build());
    }

    public Task<List<DiscordEmbed>> BuildPaginatedServerEmbeds(List<ServerResponse> servers, List<StatsResponse?> statsList)
    {
        var embeds = new List<DiscordEmbed>();

        for (int i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            var stats = statsList.ElementAtOrDefault(i);
            if (stats?.Attributes == null) continue;

            var embed = new DiscordEmbedBuilder
            {
                Title = $"🎮 {server.Attributes.Name}",
                Color = DiscordColor.Azure
            };

            AddStatFields(embed, stats);
            ConsoleExt.WriteLineWithPretext($"Embed character count: {EmbedBuilderHelper.GetEmbedCharacterCount(embed)}");
            embeds.Add(embed.Build());
        }
        
        return Task.FromResult(embeds);
    }

    private void AddStatFields(DiscordEmbedBuilder embed, StatsResponse stats)
    {
        var attr = stats.Attributes;
        var res = attr.Resources;

        string icon = EmbedBuilderHelper.GetStatusIcon(attr.CurrentState);
        embed.SafeAddField($"{icon} **Status**", attr.CurrentState, true);
        embed.SafeAddField("🧠 **Memory:**", EmbedBuilderHelper.FormatBytes(res.MemoryBytes), true);
        embed.SafeAddField("🖥️ **CPU:**", $"{res.CpuAbsolute:0.00}%", true);
        embed.SafeAddField("💽 **Disk:**", EmbedBuilderHelper.FormatBytes(res.DiskBytes), true);
        embed.SafeAddField("📥 **Network RX:**", EmbedBuilderHelper.FormatBytes(res.NetworkRxBytes), true);
        embed.SafeAddField("📤 **Network TX:**", EmbedBuilderHelper.FormatBytes(res.NetworkTxBytes), true);
        embed.SafeAddField("⏳ **Uptime:**", EmbedBuilderHelper.FormatUptime(res.Uptime), true);
    }

    private string FormatSummary(StatsResponse stats)
    {
        var attr = stats.Attributes;
        var res = attr.Resources;
        string icon = EmbedBuilderHelper.GetStatusIcon(attr.CurrentState);

        return $"{icon} **Status:** {attr.CurrentState}\n" +
               $"🧠 **Memory:** {EmbedBuilderHelper.FormatBytes(res.MemoryBytes)}\n" +
               $"🖥️ **CPU:** {res.CpuAbsolute:0.00}%\n" +
               $"💽 **Disk:** {EmbedBuilderHelper.FormatBytes(res.DiskBytes)}\n" +
               $"📥 **Network RX:** {EmbedBuilderHelper.FormatBytes(res.NetworkRxBytes)}\n" +
               $"📤 **Network TX:** {EmbedBuilderHelper.FormatBytes(res.NetworkTxBytes)}\n" +
               $"⏳ **Uptime:** {EmbedBuilderHelper.FormatUptime(res.Uptime)}";
    }
}

public static class EmbedBuilderHelper
{
    public static void SafeAddField(this DiscordEmbedBuilder builder, string name, string? value, bool inline = false)
    {
        builder.AddField(name, string.IsNullOrEmpty(value) ? "N/A" : value, inline);
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

    internal static string FormatUptime(long uptimeMs)
    {
        var uptime = TimeSpan.FromMilliseconds(uptimeMs);
        return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
    }

    public static string GetStatusIcon(string status)
    {
        return status.ToLower() switch
        {
            "offline" => "🔴",
            "running" => "🟢",
            _ => "⚪"
        };
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

