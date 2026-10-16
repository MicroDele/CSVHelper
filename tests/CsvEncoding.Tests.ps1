$ErrorActionPreference = 'Stop'

$assemblyPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\CsvReaderApp\bin\Release\net10.0-windows\CSVHelper.dll'))
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build output not found: $assemblyPath"
}
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$documentType = $assembly.GetType('CsvReaderApp.CsvDocument', $true)
$loadDocument = $documentType.GetMethod('LoadAsync')
$loadTable = $documentType.GetMethod('LoadTableAsync', [Reflection.BindingFlags]'Public,Static')
$loadTableWithEncoding = $documentType.GetMethod('LoadTableWithEncodingAsync')

# Progress callbacks run on worker threads, so keep them independent of a PowerShell runspace.
Add-Type -TypeDefinition @'
using System;
public sealed class CsvEncodingTestProgress : IProgress<(int Value, string Text)>
{
    public void Report((int Value, string Text) value) { }
}
'@
$progress = [CsvEncodingTestProgress]::new()
$token = [Threading.CancellationToken]::None
$gbk = [Text.CodePagesEncodingProvider]::Instance.GetEncoding(
    936, [Text.EncoderFallback]::ExceptionFallback, [Text.DecoderFallback]::ExceptionFallback)

function Assert-Equal($actual, $expected, [string] $message) {
    if ($actual -cne $expected) {
        throw "$message. Expected: $expected. Actual: $actual."
    }
}

function Assert-Loads([string] $path, [string] $text, [string] $encodingName = 'UTF-8') {
    $expected = $documentType.GetMethod('Parse').Invoke($null, @($text, $null))
    $document = $loadDocument.Invoke($null, @($path, $token)).GetAwaiter().GetResult()
    $loaded = $loadTableWithEncoding.Invoke($null, @($path, $progress, $token)).GetAwaiter().GetResult()
    $table = $loaded.Item1
    try {
        Assert-Equal $loaded.Item2 $encodingName "$path actual encoding"
        Assert-Equal $document.Headers.Length $expected.Headers.Length "$path document columns"
        Assert-Equal $document.Rows.Count $expected.Rows.Count "$path document rows"
        Assert-Equal $table.Columns.Count $expected.Headers.Length "$path table columns"
        Assert-Equal $table.Rows.Count $expected.Rows.Count "$path table rows (no duplicates after fallback)"
        for ($column = 0; $column -lt $expected.Headers.Length; $column++) {
            Assert-Equal $document.Headers[$column] $expected.Headers[$column] "$path document header $column"
            Assert-Equal $table.Columns[$column].ColumnName $expected.Headers[$column] "$path table header $column"
        }
        for ($row = 0; $row -lt $expected.Rows.Count; $row++) {
            Assert-Equal $document.Rows[$row].Length $expected.Rows[$row].Length "$path row width"
            for ($column = 0; $column -lt $expected.Rows[$row].Length; $column++) {
                Assert-Equal $document.Rows[$row][$column] $expected.Rows[$row][$column] "$path document cell $row,$column"
                Assert-Equal $table.Rows[$row][$column] $expected.Rows[$row][$column] "$path table cell $row,$column"
            }
        }
    }
    finally {
        $table.Dispose()
    }
}

function Assert-Fails([string] $path, [type] $exceptionType, $cancellationToken = $token) {
    foreach ($method in @($loadDocument, $loadTable, $loadTableWithEncoding)) {
        $arguments = if ($method -eq $loadDocument) { @($path, $cancellationToken) } else { @($path, $progress, $cancellationToken) }
        try {
            $null = $method.Invoke($null, $arguments).GetAwaiter().GetResult()
        }
        catch {
            $errorType = $_.Exception.GetBaseException().GetType()
            if (-not $exceptionType.IsAssignableFrom($errorType)) {
                throw "$path via $($method.Name): expected $exceptionType, got $errorType"
            }
            continue
        }
        throw "$path via $($method.Name): expected $exceptionType, but loading succeeded"
    }
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ('CSVHelper-Encoding-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($tempRoot)
try {
    $text = "name,compare,note`r`n张三,≥,`"含逗号,和`"`"引号`"`"`r`n第二行`"`r`n"
    $encodings = [ordered]@{
        'utf8' = [Text.UTF8Encoding]::new($false, $true)
        'utf8-bom' = [Text.UTF8Encoding]::new($true, $true)
        'gbk' = $gbk
        'utf16-le' = [Text.UnicodeEncoding]::new($false, $true, $true)
        'utf16-be' = [Text.UnicodeEncoding]::new($true, $true, $true)
        'utf32-le' = [Text.UTF32Encoding]::new($false, $true, $true)
        'utf32-be' = [Text.UTF32Encoding]::new($true, $true, $true)
    }
    $encodingNames = @{
        'utf8' = 'UTF-8'
        'utf8-bom' = 'UTF-8 (BOM)'
        'gbk' = 'GBK'
        'utf16-le' = 'UTF-16 LE (BOM)'
        'utf16-be' = 'UTF-16 BE (BOM)'
        'utf32-le' = 'UTF-32 LE (BOM)'
        'utf32-be' = 'UTF-32 BE (BOM)'
    }
    foreach ($entry in $encodings.GetEnumerator()) {
        $path = Join-Path $tempRoot ($entry.Key + '.csv')
        [IO.File]::WriteAllBytes($path, [byte[]]($entry.Value.GetPreamble() + $entry.Value.GetBytes($text)))
        Assert-Loads $path $text $encodingNames[$entry.Key]
    }

    foreach ($shortText in @('', 'a', "a`r`n", "a,b`r`n1,2`r`n")) {
        $path = Join-Path $tempRoot 'short.csv'
        [IO.File]::WriteAllText($path, $shortText, $encodings['utf8'])
        Assert-Loads $path $shortText
    }
    foreach ($entry in $encodings.GetEnumerator()) {
        $path = Join-Path $tempRoot 'bom-only.csv'
        [IO.File]::WriteAllBytes($path, $entry.Value.GetPreamble())
        $emptyEncoding = if ($entry.Value.GetPreamble().Length -gt 0) { $encodingNames[$entry.Key] } else { 'UTF-8' }
        Assert-Loads $path '' $emptyEncoding
    }

    # Force UTF-8 decoding to fail after multiple buffers and already parsed rows.
    $largeText = "name,compare,note`r`n" + ("ascii,1,padding`r`n" * 10000) + "张三,≥,tail`r`n"
    $path = Join-Path $tempRoot 'gbk-late-fallback.csv'
    [IO.File]::WriteAllBytes($path, $gbk.GetBytes($largeText))
    Assert-Loads $path $largeText 'GBK'

    $legacyTable = $loadTable.Invoke($null, @([string]$path, $progress, $token)).GetAwaiter().GetResult()
    try {
        Assert-Equal $legacyTable.Rows.Count 10001 'The existing table-only loader must remain compatible'
    }
    finally {
        $legacyTable.Dispose()
    }

    $path = Join-Path $tempRoot 'invalid.csv'
    [IO.File]::WriteAllBytes($path, [byte[]]@(0x61, 0x0a, 0x81))
    Assert-Fails $path ([Text.DecoderFallbackException])
    # An explicit BOM must prevent GBK fallback, and malformed Unicode must stay an error.
    [IO.File]::WriteAllBytes($path, [byte[]]@(0xef, 0xbb, 0xbf, 0x61, 0x0a, 0xa1, 0xdd))
    Assert-Fails $path ([Text.DecoderFallbackException])
    [IO.File]::WriteAllBytes($path, [byte[]]@(0xff, 0xfe, 0x00, 0xd8))
    Assert-Fails $path ([Text.DecoderFallbackException])
    [IO.File]::WriteAllText($path, "name`r`n`"unterminated", $encodings['utf8'])
    Assert-Fails $path ([FormatException])

    $cancellation = [Threading.CancellationTokenSource]::new()
    try {
        $cancellation.Cancel()
        Assert-Fails (Join-Path $tempRoot 'gbk.csv') ([OperationCanceledException]) $cancellation.Token
    }
    finally {
        $cancellation.Dispose()
    }

    # Exercise the real --inspect entry point as well as both public loading methods.
    foreach ($name in @('utf8', 'gbk', 'utf16-le')) {
        $outputPath = Join-Path $tempRoot ($name + '.txt')
        & dotnet $assemblyPath --inspect (Join-Path $tempRoot ($name + '.csv')) $outputPath
        Assert-Equal $LASTEXITCODE 0 "$name --inspect exit code"
        Assert-Equal ([IO.File]::ReadAllText($outputPath)) 'rows=1 columns=3 first=张三' "$name --inspect output"
    }
    Write-Output 'CsvEncoding tests passed (UTF-8, GBK, BOM, buffer restart, invalid data, cancellation, --inspect).'
}
finally {
    $resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
    if (-not $resolvedTempRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTempRoot) -notmatch '^CSVHelper-Encoding-[0-9a-f]{32}$') {
        throw "Refusing to remove unexpected test directory: $resolvedTempRoot"
    }
    [IO.Directory]::Delete($resolvedTempRoot, $true)
}
