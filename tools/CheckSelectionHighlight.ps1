param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$assemblyPath = Join-Path $repoRoot "CsvReaderApp\bin\$Configuration\net10.0-windows\CSVHelper.dll"

if (-not (Test-Path $assemblyPath)) {
    throw "Assembly not found: $assemblyPath"
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$formType = $assembly.GetType("CsvReaderApp.MainForm", $true)
$form = [Activator]::CreateInstance($formType, @($null))

try {
    $gridField = $formType.GetField("grid", [Reflection.BindingFlags] "Instance, NonPublic")
    $grid = [System.Windows.Forms.DataGridView]$gridField.GetValue($form)

    if ($grid.SelectionMode -ne [System.Windows.Forms.DataGridViewSelectionMode]::CellSelect) {
        throw "Expected CellSelect, got $($grid.SelectionMode)"
    }

    $expectedRowColor = [System.Drawing.Color]::FromArgb(218, 245, 230)
    $expectedCellColor = [System.Drawing.Color]::FromArgb(171, 231, 199)

    if ($grid.DefaultCellStyle.SelectionBackColor.ToArgb() -ne $expectedCellColor.ToArgb()) {
        throw "Unexpected active cell selection color: $($grid.DefaultCellStyle.SelectionBackColor)"
    }

    [string]$csvPath = Join-Path $env:TEMP "csvhelper-selection-highlight.csv"
    Set-Content -LiteralPath $csvPath -Value "A,B`r`none,two`r`nthree,four" -Encoding utf8NoBOM
    $loadCsvFile = $formType.GetMethod("LoadCsvFile", [Reflection.BindingFlags] "Instance, NonPublic")
    $loadCsvFile.Invoke($form, [object[]]@($csvPath))
    [System.Windows.Forms.Application]::DoEvents()

    if ($grid.SelectedCells.Count -ne 0) {
        throw "Newly loaded files should not have selected cells"
    }

    if ($null -ne $grid.CurrentCell) {
        throw "Newly loaded files should not have a current cell"
    }

    if (($grid.Rows | Where-Object { $_.DefaultCellStyle.BackColor.ToArgb() -ne [System.Drawing.Color]::Empty.ToArgb() }).Count -ne 0) {
        throw "Newly loaded rows should not be highlighted before a user selection"
    }

    $grid.CurrentCell = $grid.Rows[0].Cells[1]
    $grid.Rows[0].Cells[1].Selected = $true

    if ($grid.Rows[0].DefaultCellStyle.BackColor.ToArgb() -ne $expectedRowColor.ToArgb()) {
        throw "Selected row was not highlighted with the expected light color"
    }

    "Selection highlight check passed"
}
finally {
    $form.Dispose()
}
