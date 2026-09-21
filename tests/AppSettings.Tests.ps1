$ErrorActionPreference = 'Stop'

$assemblyPath = Join-Path $PSScriptRoot '..\CsvReaderApp\bin\Release\net10.0-windows\CSVHelper.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build output not found: $assemblyPath"
}

$assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
$settingsType = $assembly.GetType('CsvReaderApp.AppSettings', $true)

function Assert-Equal([object] $actual, [object] $expected, [string] $message) {
    if ($actual -ne $expected) {
        throw "$message. Expected: $expected. Actual: $actual."
    }
}

function Assert-Null([object] $actual, [string] $message) {
    # 反射返回的 null 会被 PSObject 包装，普通 -ne 判断不可靠；布尔上下文对包装 null / 真 null / 空串均为 false
    if ($actual) {
        throw "$message. Expected: <null>. Actual: $actual."
    }
}

# 隔离的临时设置文件，避免污染真实 %APPDATA%
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("csvhelper-settings-tests-" + [guid]::NewGuid().ToString('N'))
$path = [string](Join-Path $tempDir 'settings.json')

# 1. 无文件 → 默认值
$defaults = $settingsType.GetMethod('Load', [type[]]@([string])).Invoke($null, @($path))
Assert-Null $defaults.IgnoredUpdateVersion 'Missing settings file must load defaults'

# 2. 保存 + round-trip（含目录自动创建）
#（record 的 with 表达式是编译器语法糖，反射不可调，走主构造函数）
$updated = [Activator]::CreateInstance($settingsType, @([string]'1.0.1'))
Assert-Equal $updated.IgnoredUpdateVersion '1.0.1' 'Constructor must set the ignored version'
$saveWith = $settingsType.GetMethod('Save', [type[]]@([string]))
$null = $saveWith.Invoke($updated, @($path))
Assert-Equal (Test-Path -LiteralPath $path) $true 'Save must create the settings file'

$reloaded = $settingsType.GetMethod('Load', [type[]]@([string])).Invoke($null, @($path))
Assert-Equal $reloaded.IgnoredUpdateVersion '1.0.1' 'Ignored version must round-trip'

# 3. 清除忽略（恢复提示）
$cleared = [Activator]::CreateInstance($settingsType, @([string]$null))
$null = $saveWith.Invoke($cleared, @($path))
$reloaded2 = $settingsType.GetMethod('Load', [type[]]@([string])).Invoke($null, @($path))
Assert-Null $reloaded2.IgnoredUpdateVersion 'Clearing ignored version must round-trip'

# 4. 损坏文件降级为默认值
Set-Content -LiteralPath $path -Value '{ not valid json !!' -Encoding UTF8
$corrupt = $settingsType.GetMethod('Load', [type[]]@([string])).Invoke($null, @($path))
Assert-Null $corrupt.IgnoredUpdateVersion 'Corrupted settings file must degrade to defaults'

Remove-Item -Recurse -Force -LiteralPath $tempDir

Write-Output 'AppSettings tests passed.'
