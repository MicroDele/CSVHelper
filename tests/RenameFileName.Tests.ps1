$ErrorActionPreference = 'Stop'

$assemblyPath = Resolve-Path "$PSScriptRoot\..\CsvReaderApp\bin\Release\net10.0-windows\CSVHelper.dll"
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$rulesType = $assembly.GetType('CsvReaderApp.CsvFileNameRules', $true)
$method = $rulesType.GetMethod('TryCreateTargetPath', [Reflection.BindingFlags]'Static, Public, NonPublic')

function Assert-Equal {
    param(
        [string]$Expected,
        [string]$Actual,
        [string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Assert-False {
    param(
        [bool]$Actual,
        [string]$Message
    )

    if ($Actual) {
        throw "$Message Expected false."
    }
}

$targetPath = $null
$errorMessage = $null
$arguments = [object[]]@('C:\Data\Sales.csv', 'Sales 2026.xlsx', $targetPath, $errorMessage)
$renamed = [bool]$method.Invoke($null, $arguments)
Assert-Equal $true $renamed 'A valid base name should be accepted.'
Assert-Equal 'C:\Data\Sales 2026.xlsx' ([string]$arguments[2]) 'The complete file name, including its extension, should be accepted.'

$targetPath = $null
$errorMessage = $null
$arguments = [object[]]@('C:\Data\Sales.csv', '   ', $targetPath, $errorMessage)
$renamed = [bool]$method.Invoke($null, $arguments)
Assert-False $renamed 'An empty base name should be rejected.'
Assert-Equal 'Enter a file name.' ([string]$arguments[3]) 'The empty-name validation message should be clear.'

$targetPath = $null
$errorMessage = $null
$arguments = [object[]]@('C:\Data\Sales.csv', 'Sales/2026', $targetPath, $errorMessage)
$renamed = [bool]$method.Invoke($null, $arguments)
Assert-False $renamed 'A name containing a path separator should be rejected.'
Assert-Equal 'The file name contains invalid characters.' ([string]$arguments[3]) 'The invalid-name validation message should be clear.'

Write-Host 'Rename file name checks passed.'
