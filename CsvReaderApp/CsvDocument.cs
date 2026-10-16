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

    /// <summary>解析所有记录，不区分表头（每一行都是一条数据记录）。</summary>
    public static List<string[]> ParseRecords(string text, Action<int>? reportProgress = null)
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
        reportProgress?.Invoke(100);
        return records;
    }

    public static CsvDocument Parse(string text, Action<int>? reportProgress = null)
    {
        var records = ParseRecords(text, reportProgress);
        if (records.Count == 0)
        {
            return new CsvDocument(Array.Empty<string>(), new List<string[]>());
        }

        return new CsvDocument(records[0], records.Skip(1).ToList());
    }

    public static async Task<CsvDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        (await ReadWithEncodingFallbackAsync(path, async reader => Parse(await reader.ReadToEndAsync(cancellationToken)), cancellationToken)).Value;

    public static async Task<DataTable> LoadTableAsync(
        string path,
        IProgress<(int Value, string Text)> progress,
        CancellationToken cancellationToken = default) =>
        (await LoadTableWithEncodingAsync(path, progress, cancellationToken)).Table;

    public static Task<(DataTable Table, string EncodingName)> LoadTableWithEncodingAsync(
        string path,
        IProgress<(int Value, string Text)> progress,
        CancellationToken cancellationToken = default) =>
        ReadWithEncodingFallbackAsync(path, reader => LoadTableAsync(reader, progress, cancellationToken), cancellationToken);

    private static async Task<(T Value, string EncodingName)> ReadWithEncodingFallbackAsync<T>(
        string path,
        Func<StreamReader, Task<T>> read,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        var prefix = new byte[4];
        var prefixLength = await stream.ReadAtLeastAsync(prefix, prefix.Length, throwOnEndOfStream: false, cancellationToken);
        var bomEncoding = GetBomEncoding(prefix.AsSpan(0, prefixLength));

        async Task<(T Value, string EncodingName)> ReadAsync(Encoding encoding, int preambleLength)
        {
            stream.Position = preambleLength;
            using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);
            var value = await read(reader);
            var encodingName = encoding.CodePage switch
            {
                936 => "GBK",
                65001 => "UTF-8",
                1200 => "UTF-16 LE",
                1201 => "UTF-16 BE",
                12000 => "UTF-32 LE",
                12001 => "UTF-32 BE",
                _ => encoding.WebName
            };
            return (value, preambleLength > 0 ? $"{encodingName} (BOM)" : encodingName);
        }

        try
        {
            return await ReadAsync(bomEncoding ?? new UTF8Encoding(false, true), bomEncoding?.GetPreamble().Length ?? 0);
        }
        catch (DecoderFallbackException) when (bomEncoding is null)
        {
            // A BOM-less legacy CSV may use GBK. Restart both decoding and parsing from the beginning.
            var gbkEncoding = CodePagesEncodingProvider.Instance.GetEncoding(
                936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!;
            return await ReadAsync(gbkEncoding, 0);
        }
    }

    private static Encoding? GetBomEncoding(ReadOnlySpan<byte> prefix)
    {
        // Check UTF-32 before UTF-16 because their little-endian BOMs share a prefix.
        if (prefix.StartsWith(new byte[] { 0xff, 0xfe, 0x00, 0x00 }))
            return new UTF32Encoding(false, true, true);
        if (prefix.StartsWith(new byte[] { 0x00, 0x00, 0xfe, 0xff }))
            return new UTF32Encoding(true, true, true);
        if (prefix.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
            return new UTF8Encoding(true, true);
        if (prefix.StartsWith(new byte[] { 0xff, 0xfe }))
            return new UnicodeEncoding(false, true, true);
        if (prefix.StartsWith(new byte[] { 0xfe, 0xff }))
            return new UnicodeEncoding(true, true, true);
        return null;
    }

    private static async Task<DataTable> LoadTableAsync(
        StreamReader reader,
        IProgress<(int Value, string Text)> progress,
        CancellationToken cancellationToken)
    {
        var stream = reader.BaseStream;

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

    /// <summary>
    /// 从预解析的数据（表头+行）创建 DataTable。
    /// 行中超出列数的字段自动补列，不足的填空字符串。
    /// </summary>
    private static DataTable CreateTableWithHeaders(string[] headers)
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

    /// <summary>用表头建表，数据行字段数超过列数时自动追加列（Paste Data 场景）。</summary>
    public static DataTable CreateTable(string[] headers, List<string[]> rows)
    {
        var table = CreateTableWithHeaders(headers);
        foreach (var fields in rows)
        {
            while (table.Columns.Count < fields.Length)
            {
                table.Columns.Add($"Column {table.Columns.Count + 1}", typeof(string));
            }
            var row = table.NewRow();
            for (var i = 0; i < fields.Length; i++)
            {
                row[i] = fields[i];
            }
            table.Rows.Add(row);
        }

        return table;
    }

    /// <summary>
    /// 用表头建表，列数严格 = 表头数：数据行多余字段丢弃、不足补空字符串。
    /// 用于 Paste Headers——表头决定最终列数，原有数据按列索引保留。
    /// </summary>
    public static DataTable CreateTableFixedColumns(string[] headers, List<string[]> rows)
    {
        var table = CreateTableWithHeaders(headers);
        var columnCount = table.Columns.Count;
        foreach (var fields in rows)
        {
            var row = table.NewRow();
            for (var i = 0; i < columnCount; i++)
            {
                row[i] = i < fields.Length ? fields[i] : string.Empty;
            }
            table.Rows.Add(row);
        }

        return table;
    }

    public string ToCsvText()
    {
        var lines = new List<string> { string.Join(",", Headers.Select(EscapeField)) };
        lines.AddRange(Rows.Select(row => string.Join(",", row.Select(EscapeField))));
        return string.Join("\r\n", lines);
    }

    /// <summary>将数据行序列化为 CSV 文本（不含表头行）。</summary>
    public static string RowsToCsvText(List<string[]> rows) =>
        string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(EscapeField))));

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
