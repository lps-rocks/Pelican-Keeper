using System.Text.RegularExpressions;

namespace Pelican_Keeper;

public class ServerMarkdown
{
    string ParseTemplateWithRegex(string template, Dictionary<string, string> vars)
    {
        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            string key = match.Groups[1].Value;
            return vars.TryGetValue(key, out var var) ? var : match.Value; // leaves unknown keys untouched
        });
    }
}