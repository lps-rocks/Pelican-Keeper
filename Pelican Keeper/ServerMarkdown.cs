using System.Text.RegularExpressions;

namespace Pelican_Keeper;

public class ServerMarkdown
{
    /// <summary>
    /// Parses a template with regex to replace placeholders with values
    /// </summary>
    /// <param name="template">string template</param>
    /// <param name="vars">dictionary of variables</param>
    /// <returns>string of parsed template</returns>
    string ParseTemplateWithRegex(string template, Dictionary<string, string> vars)
    {
        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            string key = match.Groups[1].Value;
            return vars.TryGetValue(key, out var var) ? var : match.Value; // leaves unknown keys untouched
        });
    }
}