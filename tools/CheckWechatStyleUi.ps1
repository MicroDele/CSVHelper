param()

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$themePath = Join-Path $repoRoot "CsvReaderApp\UiTheme.cs"
$mainPath = Join-Path $repoRoot "CsvReaderApp\MainForm.cs"
$dialogPath = Join-Path $repoRoot "CsvReaderApp\MultiSortDialog.cs"

foreach ($path in @($themePath, $mainPath, $dialogPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing expected UI file: $path"
    }
}

$themeText = Get-Content -Raw -LiteralPath $themePath
$mainText = Get-Content -Raw -LiteralPath $mainPath
$dialogText = Get-Content -Raw -LiteralPath $dialogPath

$requiredThemeSnippets = @(
    "WeChatGreen = Color.FromArgb(7, 193, 96)",
    "ApplyIconButton(",
    "CreateIcon("
)

foreach ($snippet in $requiredThemeSnippets) {
    if (-not $themeText.Contains($snippet)) {
        throw "Missing WeChat-style theme support: $snippet"
    }
}

$requiredMainSnippets = @(
    "private readonly ToolTip toolTip = new();",
    "UiTheme.ApplyIconButton(openButton",
    "UiTheme.ApplyIconButton(saveButton",
    "UiTheme.ApplyIconButton(multiSortButton",
    "UiTheme.ApplyIconButton(prevMatchButton",
    "UiTheme.ApplyIconButton(nextMatchButton"
)

foreach ($snippet in $requiredMainSnippets) {
    if (-not $mainText.Contains($snippet)) {
        throw "MainForm is missing icon-button style: $snippet"
    }
}

$requiredDialogSnippets = @(
    "private readonly ToolTip toolTip = new();",
    "TableLayoutPanel",
    "FlowLayoutPanel",
    "ClientSize = new Size(640, 420)",
    "UiTheme.ApplyIconButton(addButton",
    "UiTheme.ApplyIconButton(removeButton",
    "UiTheme.ApplyIconButton(upButton",
    "UiTheme.ApplyIconButton(downButton",
    "UiTheme.ApplyIconButton(clearButton",
    "UiTheme.ApplyIconButton(okButton",
    "UiTheme.ApplyIconButton(cancelButton"
)

foreach ($snippet in $requiredDialogSnippets) {
    if (-not $dialogText.Contains($snippet)) {
        throw "MultiSortDialog is missing resilient WeChat-style layout: $snippet"
    }
}

if ($dialogText.Contains(".SetBounds(")) {
    throw "MultiSortDialog still uses fixed SetBounds layout"
}

"WeChat-style UI check passed"
