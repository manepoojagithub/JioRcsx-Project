using System.Text.RegularExpressions;

namespace JioCxRcsWrapper.Application.Campaigns;

public sealed partial class ContactCsvParser : IContactCsvParser
{
    public ParsedContactsResult Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return ParsedContactsResult.Failed(["Contact required"]);
        }

        var rows = csv.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (rows.Count == 0)
        {
            return ParsedContactsResult.Failed(["Contact required"]);
        }

        // Determine headers
        var headers = rows[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
        var hasHeaderRow = string.Equals(headers[0], "MobileNumber", StringComparison.OrdinalIgnoreCase);
        
        if (hasHeaderRow)
        {
            rows.RemoveAt(0);
        }
        else
        {
            // Default headers if none provided
            headers = new string[headers.Length];
            headers[0] = "MobileNumber";
            for (int i = 1; i < headers.Length; i++) headers[i] = $"var{i}";
        }

        var errors = new List<string>();
        var parsedContacts = new List<ParsedContactData>();

        foreach (var row in rows)
        {
            var columns = row.Split(',').Select(c => c.Trim().Trim('"')).ToArray();
            var mobile = columns[0];

            if (string.IsNullOrWhiteSpace(mobile))
            {
                errors.Add("Contact required");
                continue;
            }

            var normalized = mobile.StartsWith('+') ? mobile : $"+{mobile}";
            if (!MobileRegex().IsMatch(normalized))
            {
                errors.Add($"Invalid mobile number: {mobile}");
                continue;
            }

            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < Math.Min(columns.Length, headers.Length); i++)
            {
                variables[headers[i]] = columns[i];
            }

            parsedContacts.Add(new ParsedContactData(normalized, variables));
        }

        return errors.Count == 0
            ? ParsedContactsResult.Success(parsedContacts)
            : ParsedContactsResult.Failed(errors);
    }

    [GeneratedRegex("^\\+[1-9][0-9]{7,14}$")]
    private static partial Regex MobileRegex();
}
