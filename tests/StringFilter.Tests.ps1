$ErrorActionPreference = 'Stop'

$assemblyPath = Join-Path $PSScriptRoot '..\CsvReaderApp\bin\Release\net10.0-windows\CSVHelper.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build output not found: $assemblyPath"
}

$assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
$filterType = $assembly.GetType('CsvReaderApp.StringFilter', $true)

function New-StringFilter([string] $columnName, [string] $operator, [string] $value) {
    return [Activator]::CreateInstance($filterType, @($columnName, $operator, $value))
}

function Get-FilteredCount([string] $columnName, [string] $operator, [string] $value) {
    $filter = New-StringFilter $columnName $operator $value
    $expression = $filter.ToRowFilterExpression()
    $table.DefaultView.RowFilter = $expression
    return $table.DefaultView.Count
}

function Assert-Equal([object] $actual, [object] $expected, [string] $message) {
    if ($actual -ne $expected) {
        throw "$message. Expected: $expected. Actual: $actual."
    }
}

$table = [System.Data.DataTable]::new()
[void]$table.Columns.Add('Name', [string])
[void]$table.Columns.Add('A]B', [string])

foreach ($pair in @(
    @('Apple', 'one'),
    @('Mango', "O'Brien"),
    @("O'Brien", 'three'),
    @('Zebra', 'four'),
    @('100% match', '[brackets]')
)) {
    $row = $table.NewRow()
    $row['Name'] = $pair[0]
    $row['A]B'] = $pair[1]
    [void]$table.Rows.Add($row)
}

Assert-Equal (Get-FilteredCount 'Name' '=' "O'Brien") 1 'Equality must escape apostrophes'
Assert-Equal (Get-FilteredCount 'Name' '>' 'Mango') 2 'Greater-than must use string comparison'
Assert-Equal (Get-FilteredCount 'Name' '<' 'Mango') 2 'Less-than must use string comparison'
Assert-Equal (Get-FilteredCount 'Name' '>=' 'Mango') 3 'Greater-than-or-equal must include equality'
Assert-Equal (Get-FilteredCount 'Name' '<=' 'Mango') 3 'Less-than-or-equal must include equality'
Assert-Equal (Get-FilteredCount 'A]B' '=' "O'Brien") 1 'Column names containing ] must be escaped'
Assert-Equal (Get-FilteredCount 'Name' 'Contains' 'bra') 1 'Contains must find a substring'
Assert-Equal (Get-FilteredCount 'Name' 'Contains' '%') 1 'Contains must treat percent as literal text'
Assert-Equal (Get-FilteredCount 'A]B' 'Contains' '[') 1 'Contains must treat brackets as literal text'

$table.DefaultView.RowFilter = ''
Assert-Equal $table.DefaultView.Count 5 'Clearing the filter must restore every row'

$table.DefaultView.Sort = '[Name] ASC'
$table.DefaultView.RowFilter = (New-StringFilter 'Name' '>' 'Mango').ToRowFilterExpression()
Assert-Equal $table.DefaultView[0]['Name'] "O'Brien" 'Sorting must remain active while filtering'

Write-Output 'StringFilter tests passed.'
