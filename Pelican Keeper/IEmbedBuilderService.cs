using DSharpPlus.Entities;

namespace Pelican_Keeper;
using static TemplateClasses;

public interface IEmbedBuilderService
{
    Task<DiscordEmbed> BuildSingleServerEmbed(ServerResponse server, StatsResponse stats);
    Task<DiscordEmbed> BuildMultiServerEmbed(List<ServerResponse> servers, List<StatsResponse?> statsList);
    Task<List<DiscordEmbed>> BuildPaginatedServerEmbeds(List<ServerResponse> servers, List<StatsResponse?> statsList);
}
