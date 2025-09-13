using System.Text.Json;

namespace Pelican_Keeper;

using static ConsoleExt;

public static class FileManager
{
    /// <summary>
    /// Gets the file path if it exists in the current execution directory or in the Pelican Keeper directory.
    /// </summary>
    /// <param name="path">The File path to check</param>
    /// <returns>The File path or empty string</returns>
    public static string GetFilePath(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }
        return File.Exists(Path.Combine("/Pelican Keeper/",path)) ? Path.Combine("/Pelican Keeper/",path) : string.Empty;
    }

    /// <summary>
    /// Creates a default Secrets.json file in the current execution directory.
    /// </summary>
    private static async Task CreateSecretsFile()
    {
        WriteLineWithPretext("Secrets.json not found. Creating default one.", OutputType.Warning);
        await using var secretsFile = File.Create("Secrets.json");
        string defaultSecrets = new string("{\n  \"ClientToken\": \"YOUR_CLIENT_TOKEN\",\n  \"ServerToken\": \"YOUR_SERVER_TOKEN\",\n  \"ServerUrl\": \"YOUR_BASIC_SERVER_URL\",\n  \"BotToken\": \"YOUR_DISCORD_BOT_TOKEN\",\n  \"ChannelIds\": [THE_CHANNEL_ID_YOU_WANT_THE_BOT_TO_POST_IN],\n  \"ExternalServerIp\": \"YOUR_EXTERNAL_SERVER_IP\"\n}");
        await using var writer = new StreamWriter(secretsFile);
        await writer.WriteAsync(defaultSecrets);
        WriteLineWithPretext("Created default Secrets.json. Please fill out the values.", OutputType.Warning);
    }

    /// <summary>
    /// Creates a default Config.json file in the current execution directory.
    /// </summary>
    private static async Task CreateConfigFile()
    {
        await using var configFile = File.Create("Config.json");
        var defaultConfig = new string("{\n  \"InternalIpStructure\": \"192.168.*.*\",\n  \"MessageFormat\": \"Consolidated\",\n  \"MessageSorting\": \"Name\",\n  \"MessageSortingDirection\": \"Ascending\",\n  \"IgnoreOfflineServers\": false,\n  \"ServersToIgnore\": [\"UUIDS HERE\"],\n  \n  \"JoinableIpDisplay\": false,\n  \"PlayerCountDisplay\": false,\n  \"ServersToMonitor\": [\"UUIDS HERE\"],\n  \n  \"AutomaticShutdown\": true,\n  \"ServersToAutoShutdown\": [\"UUIDS HERE\"],\n  \"EmptyServerTimeout\": \"00:01:00\",\n  \"AllowUserServerStartup\": true,\n  \"AllowServerStartup\": [\"UUIDS HERE\"],\n  \"UsersAllowedToStartServers\": [\"USERID HERE\"],\n  \"AllowUserServerStopping\": true,\n  \"AllowServerStopping\": [\"UUIDS HERE\"],\n  \"UsersAllowedToStopServers\": [\"USERID HERE\"],\n  \n  \"ContinuesMarkdownRead\": true,\n  \"ContinuesGamesToMonitorRead\": true,\n  \"MarkdownUpdateInterval\": 30,\n  \"ServerUpdateInterval\": 10,\n  \n  \"LimitServerCount\": false,\n  \"MaxServerCount\": 10,\n  \"ServersToDisplay\": [\"UUIDS HERE\"],\n  \n  \"Debug\": true,\n  \"DryRun\": false\n}");
        await using var writer = new StreamWriter(configFile);
        await writer.WriteAsync(defaultConfig);
    }

    public static async Task CreateMessageMarkdownFile()
    {
        await using var messageMarkdownFile = File.Create("MessageMarkdown.txt");
        var defaultConfig = new string("[Title]🎮 **{{ServerName}}**[/Title]\n\n{{StatusIcon}} **Status:** {{Status}}\n🖥️ **CPU:** {{Cpu}}\n🧠 **Memory:** {{Memory}}\n💽 **Disk:** {{Disk}}\n📥 **NetworkRX:** {{NetworkRx}}\n📤 **NetworkTX:** {{NetworkTx}}\n⏳ **Uptime:** {{Uptime}}");
        await using var writer = new StreamWriter(messageMarkdownFile);
        await writer.WriteAsync(defaultConfig);
    }
    
    public static async Task<TemplateClasses.Secrets?> ReadSecretsFile()
    {
        string secretsPath = GetFilePath("Secrets.json");
        
        if (secretsPath == String.Empty)
        {
            await CreateSecretsFile();
            return null;
        }

        TemplateClasses.Secrets? secrets;
        try
        {
            var secretsJson = await File.ReadAllTextAsync(secretsPath);
            secrets = JsonSerializer.Deserialize<TemplateClasses.Secrets>(secretsJson)!;
        }
        catch (Exception ex)
        {
            WriteLineWithPretext("Failed to load secrets. Check that the Secrets file is filled out and is in the correct format. Check Secrets.json", OutputType.Error, ex);
            Environment.Exit(0);
            return null;
        }

        Program.Secrets = secrets;
        return secrets;
    }
    
    public static async Task<TemplateClasses.Config?> ReadConfigFile()
    {
        string configPath = GetFilePath("Config.json");
        
        if (configPath == String.Empty)
        {
            await CreateConfigFile();
        }
        
        TemplateClasses.Config? config;
        try
        {
            var configJson = await File.ReadAllTextAsync(configPath);
            config = JsonSerializer.Deserialize<TemplateClasses.Config>(configJson);
        }
        catch (Exception ex)
        {
            WriteLineWithPretext("Failed to load config. Check if nothing is misspelled and you used the correct options", OutputType.Error, ex);
            Environment.Exit(0);
            return null;
        }
        
        if (config == null)
        {
            WriteLineWithPretext("Config file is empty or not in the correct format. Please check Config.json", OutputType.Error);
            Environment.Exit(0);
            return null;
        }
        
        Program.Config = config;
        return config;
    }
    
    public static async Task<List<TemplateClasses.GamesToMonitor>?> ReadGamesToMonitorFile()
    {
        string gameCommPath = GetFilePath("GamesToMonitor.json");
        
        if (gameCommPath == String.Empty)
        {
            WriteLineWithPretext("GamesToMonitor.json not found. Creating default one.", OutputType.Error);//TODO: Add default creation of GamesToMonitor.json
            return null;
        }

        try
        {
            var gameCommJson = await File.ReadAllTextAsync(gameCommPath);
            var gameComms = JsonSerializer.Deserialize<List<TemplateClasses.GamesToMonitor>>(gameCommJson);
            return gameComms;
        }
        catch (Exception ex)
        {
            WriteLineWithPretext("Failed to load GamesToMonitor.json. Check if nothing is misspelled and you used the correct options", OutputType.Error, ex);
            return null;
        }
    }
}