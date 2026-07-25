param()

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$mainPath = Join-Path $repoRoot "CsvReaderApp\MainForm.cs"

$mainText = Get-Content -Raw -LiteralPath $mainPath

$requiredSnippets = @(
    "private const int SortGlyphReservedHeaderWidth = 30;",
    "private const int MinimumSortableHeaderWidth = 56;",
    "EnsureSortableHeaderWidth(column);",
    "TextRenderer.MeasureText(headerText, headerFont).Width",
    "column.MinimumWidth = Math.Max(column.MinimumWidth, requiredWidth);"
)

foreach ($snippet in $requiredSnippets) {
    if (-not $mainText.Contains($snippet)) {
        throw "Missing sort glyph header width safeguard: $snippet"
    }
}

"Sort glyph header width check passed"
