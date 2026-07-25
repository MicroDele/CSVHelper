using System.Text;

namespace CsvReaderApp;

public sealed class CsvDocument
{
    public CsvDocument(string[] headers, List<string[]> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public string[] Headers { get; }

    public List<string[]> Rows { get; }

    public static CsvDocument Parse(string text)
    {
        var records = new List<string[]>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(ch);
                i++;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                i++;
                continue;
            }

            if (ch == ',')
            {
                record.Add(field.ToString());
                field.Clear();
                i++;
                continue;
            }

            if (ch is '\r' or '\n')
            {
                record.Add(field.ToString());
                field.Clear();
                records.Add(record.ToArray());
                record.Clear();

                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i += 2;
                }
                else
                {
                    i++;
                }

                continue;
            }

            field.Append(ch);
            i++;
        }

        if (inQuotes)
        {
            throw new FormatException("CSV parse error: unterminated quoted field.");
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record.ToArray());
        }

        if (records.Count == 0)
        {
            return new CsvDocument(Array.Empty<string>(), new List<string[]>());
        }

        return new CsvDocument(records[0], records.Skip(1).ToList());
    }

    public string ToCsvText()
    {
        var lines = new List<string> { string.Join(",", Headers.Select(EscapeField)) };
        lines.AddRange(Rows.Select(row => string.Join(",", row.Select(EscapeField))));
        return string.Join("\r\n", lines);
    }

    private static string EscapeField(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value}\"" : value;
    }
}
