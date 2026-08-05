namespace CsvReaderApp;

internal sealed record StringFilter(string ColumnName, string Operator, string Value)
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.Ordinal)
    {
        "=",
        ">",
        "<",
        ">=",
        "<=",
        "Contains"
    };

    public string ToRowFilterExpression()
    {
        if (!SupportedOperators.Contains(Operator))
        {
            throw new ArgumentException($"Unsupported filter operator: {Operator}", nameof(Operator));
        }

        var escapedColumnName = ColumnName.Replace("\\", "\\\\").Replace("]", "\\]");
        if (Operator == "Contains")
        {
            var escapedLikeValue = Value
                .Replace("]", "[]]")
                .Replace("[", "[[]")
                .Replace("*", "[*]")
                .Replace("%", "[%]")
                .Replace("'", "''");
            return $"[{escapedColumnName}] LIKE '%{escapedLikeValue}%'";
        }

        var escapedValue = Value.Replace("'", "''");
        return $"[{escapedColumnName}] {Operator} '{escapedValue}'";
    }
}
