using System.Text.RegularExpressions;

namespace Pelican_Keeper;

public static class ServerMarkdown
{
    private record PreprocessedTemplate(string Body, Dictionary<string, string> Tags);

    /// <summary>
    /// Processes [Tag]...[/Tag] blocks, replaces placeholders inside them,
    /// and removes them from the final template body.
    /// </summary>
    /// <param name="model">The template model to use</param>
    /// <returns>A preprocessed template</returns>
    private static PreprocessedTemplate PreprocessTemplateTags(object model)
    {
        string templateText = File.ReadAllText("MessageMarkdown.txt");
        
        var tagDict = new Dictionary<string, string>();

        var tagRegex = new Regex(@"\[(\w+)](.*?)\[/\1]", RegexOptions.Singleline);
    
        string strippedTemplate = tagRegex.Replace(templateText, match =>
        {
            string tag = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            string rendered = ReplacePlaceholders(content, model);

            tagDict[tag] = rendered.Trim();
            return ""; // removes the tagged content from the final template body
        });

        return new PreprocessedTemplate(strippedTemplate.Trim(), tagDict);
    }

    /// <summary>
    /// Replaces {{VariableName}} placeholders in text using reflection on the model.
    /// </summary>
    /// <param name="text">Input text</param>
    /// <param name="model">The template model to use</param>
    /// <returns>The text with placeholders replaced</returns>
    private static string ReplacePlaceholders(string text, object model)
    {
        return Regex.Replace(text, @"\{\{(\w+)\}\}", match =>
        {
            var propName = match.Groups[1].Value;
            var prop = model.GetType().GetProperty(propName);
            if (prop != null)
            {
                var value = prop.GetValue(model);
                return value?.ToString() ?? "";
            }
            return match.Value; // leaves the placeholder as-is if not found
        });
    }

    /// <summary>
    /// Parses the message template and returns the final message and server title.
    /// </summary>
    /// <param name="serverResponse">The server response</param>
    /// <param name="statsResponse">The stats' response</param>
    /// <returns>A tuple containing the final message and server title</returns>
    public static (string message, string serverName) ParseTemplate(
        TemplateClasses.ServerResponse serverResponse,
        TemplateClasses.StatsResponse statsResponse)
    {
        var viewModel = new TemplateClasses.ServerViewModel
        {
            ServerName = serverResponse.Attributes.Name,
            Status = statsResponse.Attributes.CurrentState,
            StatusIcon = EmbedBuilderHelper.GetStatusIcon(statsResponse.Attributes.CurrentState),
            Cpu = $"{statsResponse.Attributes.Resources.CpuAbsolute:0.00}%",
            Memory = EmbedBuilderHelper.FormatBytes(statsResponse.Attributes.Resources.MemoryBytes),
            Disk = EmbedBuilderHelper.FormatBytes(statsResponse.Attributes.Resources.DiskBytes),
            NetworkRx = EmbedBuilderHelper.FormatBytes(statsResponse.Attributes.Resources.NetworkRxBytes),
            NetworkTx = EmbedBuilderHelper.FormatBytes(statsResponse.Attributes.Resources.NetworkTxBytes),
            Uptime = EmbedBuilderHelper.FormatUptime(statsResponse.Attributes.Resources.Uptime)
        };

        var result = PreprocessTemplateTags(viewModel);
        var serverName = result.Tags.GetValueOrDefault("Title", "Default Title");
        var message = ReplacePlaceholders(result.Body, viewModel);

        ConsoleExt.WriteLineWithPretext($"Server: {viewModel.ServerName}");
        ConsoleExt.WriteLineWithPretext($"Message Character Count: {message.Length}");
        ConsoleExt.WriteLineWithPretext($"Parsed Message: {message}");

        return (message, serverName);
    }
}