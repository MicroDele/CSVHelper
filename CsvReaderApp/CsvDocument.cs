using System.Data;
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

    public static CsvDocument Parse(string text, Action<int>? reportProgress = null)
    {
        var records = new List<string[]>();
        var parser = new CsvRecordParser();
        var lastReportedProgress = -1;

        for (var index = 0; index < text.Length; index++)
        {
            if ((index & 0x1fff) == 0)
            {
                var progress = text.Length == 0 ? 100 : index * 100 / text.Length;
                if (progress != lastReportedProgress)
                {
                    reportProgress?.Invoke(progress);
                    lastReportedProgress = progress;
                }
            }

            parser.Append(text[index], records.Add);
        }

        parser.Complete(records.Add);

        if (records.Count == 0)
        {
            reportProgress?.Invoke(100);
            return new CsvDocument(Array.Empty<string>(), new List<string[]>());
        }

        reportProgress?.Invoke(100);
        return new CsvDocument(records[0], records.Skip(1).ToList());
    }

    public static async Task<DataTable> LoadTableAsync(
        string path,
        IProgress<(int Value, string Text)> progress,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024);

        var parser = new CsvRecordParser();
        var buffer = new char[64 * 1024];
        DataTable? table = null;
        var rowCount = 0;
        var lastProgress = -1;

        void AddRecord(string[] record)
        {
            if (table is null)
            {
                table = CreateTable(record);
                return;
            }

            while (table.Columns.Count < record.Length)
            {
                table.Columns.Add($"Column {table.Columns.Count + 1}", typeof(string));
            }

            var dataRow = table.NewRow();
            for (var columnIndex = 0; columnIndex < record.Length; columnIndex++)
            {
                dataRow[columnIndex] = record[columnIndex];
            }

            table.Rows.Add(dataRow);
            rowCount++;
        }

        progress.Report((0, "Reading and parsing CSV... 0%"));
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = 0; index < read; index++)
            {
                parser.Append(buffer[index], AddRecord);
            }

            var value = stream.Length == 0 ? 100 : Math.Min(99, (int)(stream.Position * 100 / stream.Length));
            if (value != lastProgress)
            {
                progress.Report((value, $"Reading and parsing CSV... {value}% ({rowCount:N0} rows)"));
                lastProgress = value;
            }
        }

        parser.Complete(AddRecord);
        progress.Report((100, $"Preparing table... {rowCount:N0} rows"));
        return table ?? new DataTable();
    }

    private static DataTable CreateTable(string[] headers)
    {
        var table = new DataTable();
        foreach (var header in headers)
        {
            var name = string.IsNullOrWhiteSpace(header) ? $"Column {table.Columns.Count + 1}" : header;
            if (table.Columns.Contains(name))
            {
                name = $"{name} {table.Columns.Count + 1}";
            }

            table.Columns.Add(name, typeof(string));
        }

        return table;
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

    private sealed class CsvRecordParser
    {
        private readonly List<string> record = new();
        private readonly StringBuilder field = new();
        private bool inQuotes;
        private bool quotePending;
        private bool skipLineFeed;

        public void Append(char ch, Action<string[]> recordHandler)
        {
            if (skipLineFeed)
            {
                skipLineFeed = false;
                if (ch == '\n')
                {
                    return;
                }
            }

            if (inQuotes)
            {
                if (quotePending)
                {
                    if (ch == '"')
                    {
                        field.Append('"');
                        quotePending = false;
                        return;
                    }

                    inQuotes = false;
                    quotePending = false;
                }
                else if (ch == '"')
                {
                    quotePending = true;
                    return;
                }
                else
                {
                    field.Append(ch);
                    return;
                }
            }

            if (ch == '"')
            {
                inQuotes = true;
                return;
            }

            if (ch == ',')
            {
                AddField();
                return;
            }

            if (ch is '\r' or '\n')
            {
                AddField();
                recordHandler(record.ToArray());
                record.Clear();
                skipLineFeed = ch == '\r';
                return;
            }

            field.Append(ch);
        }

        public void Complete(Action<string[]> recordHandler)
        {
            if (inQuotes && !quotePending)
            {
                throw new FormatException("CSV parse error: unterminated quoted field.");
            }

            inQuotes = false;
            quotePending = false;
            if (field.Length > 0 || record.Count > 0)
            {
                AddField();
                recordHandler(record.ToArray());
                record.Clear();
            }
        }

        private void AddField()
        {
            record.Add(field.ToString());
            field.Clear();
        }
    }
}
