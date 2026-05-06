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

        if (string.Equals(rows[0].Trim().Trim('"'), "MobileNumber", StringComparison.OrdinalIgnoreCase))
        {
            rows.RemoveAt(0);
        }

        var errors = new List<string>();
        var mobileNumbers = new List<string>();

        foreach (var row in rows)
        {
            var value = row.Split(',')[0].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add("Contact required");
                continue;
            }

            var normalized = value.StartsWith('+') ? value : $"+{value}";
            if (!MobileRegex().IsMatch(normalized))
            {
                errors.Add($"Invalid mobile number: {value}");
                continue;
            }

            mobileNumbers.Add(normalized);
        }

        return errors.Count == 0
            ? ParsedContactsResult.Success(mobileNumbers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            : ParsedContactsResult.Failed(errors);
    }

    [GeneratedRegex("^\\+[1-9][0-9]{7,14}$")]
    private static partial Regex MobileRegex();
}
