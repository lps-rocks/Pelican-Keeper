using Pelican_Keeper;

namespace Pelican_Keeper_Unit_Testing;

public class TestConfigCreator
{
    public static TemplateClasses.Config CreateDefaultConfigInstance()
    {
        return new TemplateClasses.Config
        {
            InternalIpStructure = "192.168.*.*",
            MessageFormat = TemplateClasses.MessageFormat.Consolidated,
            MessageSorting = TemplateClasses.MessageSorting.Name,
            MessageSortingDirection = TemplateClasses.MessageSortingDirection.Ascending,
            IgnoreOfflineServers = false,
            ServersToIgnore = ["UUIDS HERE"],
            
            JoinableIpDisplay = true,
            PlayerCountDisplay = true,
            
            AutomaticShutdown = true,
            ServersToAutoShutdown = ["UUIDS HERE"],
            EmptyServerTimeout = "00:01:00",
            AllowUserServerStartup = true,
            AllowServerStartup = ["UUIDS HERE"],
            
            ContinuesMarkdownRead = true,
            ContinuesGamesToMonitorRead = true,
            MarkdownUpdateInterval = 30,
            ServerUpdateInterval = 10,
            
            LimitServerCount = false,
            MaxServerCount = 10,
            ServersToDisplay = ["UUIDS HERE"],
            
            Debug = true,
            DryRun = true
        };
    }

    public static TemplateClasses.GamesToMonitor CreateDefaultServersToMonitor()
    {
        return new TemplateClasses.GamesToMonitor
        {
            Game = "GAME_NAME_HERE",
            Protocol = TemplateClasses.CommandExecutionMethod.Rcon,
            
        };
    }
}