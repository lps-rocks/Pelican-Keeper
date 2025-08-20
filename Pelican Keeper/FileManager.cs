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
            WriteLineWithPretext(path);
            return path;
        }
        return File.Exists(Path.Combine("/Pelican Keeper/",path)) ? Path.Combine("/Pelican Keeper/",path) : string.Empty;
    }

    /// <summary>
    /// Creates a default Secrets.json file in the current execution directory.
    /// </summary>
    public static async Task CreateSecretsFile()
    {
        WriteLineWithPretext("Secrets.json not found. Creating default one.", OutputType.Warning);
        await using var secretsFile = File.Create("Secrets.json");
        string defaultSecrets = new string("{\n  \"ClientToken\": \"YOUR_CLIENT_TOKEN\",\n  \"ServerToken\": \"YOUR_SERVER_TOKEN\",\n  \"ServerUrl\": \"YOUR_BASIC_SERVER_URL\",\n  \"BotToken\": \"YOUR_DISCORD_BOT_TOKEN\",\n  \"ChannelId\": \"THE_CHANNEL_ID_YOU_WANT_THE_BOT_TO_POST_IN\",\n  \"ExternalServerIP\": \"YOUR_EXTERNAL_SERVER_IP\"\n}");
        await using var writer = new StreamWriter(secretsFile);
        await writer.WriteAsync(defaultSecrets);
        WriteLineWithPretext("Created default Secrets.json. Please fill out the values.", OutputType.Warning);
    }

    /// <summary>
    /// Creates a default Config.json file in the current execution directory.
    /// </summary>
    public static async Task CreateConfigFile()
    {
        await using var configFile = File.Create("Config.json");
        var defaultConfig = new string("{\n  \"ConsolidateEmbeds\": true,\n  \"Paginate\": false\n}");
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
            return new TemplateClasses.Secrets();
        }

        TemplateClasses.Secrets? secrets;
        try
        {
            var secretsJson = await File.ReadAllTextAsync(secretsPath);
            secrets = JsonSerializer.Deserialize<TemplateClasses.Secrets>(secretsJson)!;
        }
        catch (Exception ex)
        {
            WriteLineWithPretext("Failed to load secrets. Check that the Secrets file is filled out and nothing is misspelled. Check Secrets.json", OutputType.Error, ex);
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
            return null;
        }
        
        Program.Config = config;
        return config;
    }
}