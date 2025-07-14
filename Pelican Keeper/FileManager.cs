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
}