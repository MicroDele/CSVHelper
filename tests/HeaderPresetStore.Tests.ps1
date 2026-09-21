$ErrorActionPreference = 'Stop'

$assemblyPath = Join-Path $PSScriptRoot '..\CsvReaderApp\bin\Release\net10.0-windows\CSVHelper.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build output not found: $assemblyPath"
}

$assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
$storeType = $assembly.GetType('CsvReaderApp.HeaderPresetStore', $true)

function Assert-Equal([object] $actual, [object] $expected, [string] $message) {
    if ($actual -ne $expected) {
        throw "$message. Expected: $expected. Actual: $actual."
    }
}

function New-Store([string] $filePath) {
    return [Activator]::CreateInstance($storeType, @($filePath))
}

# 隔离的临时存储文件，避免污染真实 %APPDATA%
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("csvhelper-preset-tests-" + [guid]::NewGuid().ToString('N'))
$storePath = Join-Path $tempDir 'header-presets.json'
$store = New-Store $storePath

# 1. 全新存储加载为空
Assert-Equal $store.Load().Count 0 'Fresh store must load empty'
Assert-Equal (Test-Path -LiteralPath $storePath) $false 'Load must not create the file'

# 2. 保存 + round-trip（新实例从磁盘读）
Assert-Equal $store.SavePreset('人员信息', [string[]]@('Name', 'Age')) $true 'Save must return true'
$reloaded = (New-Store $storePath).Load()
Assert-Equal $reloaded.Count 1 'Saved preset must round-trip'
Assert-Equal $reloaded[0].Name '人员信息' 'Preset name must round-trip'
Assert-Equal ($reloaded[0].Headers -join '|') 'Name|Age' 'Preset headers must round-trip'

# 3. 同名保存覆盖而非追加
Assert-Equal $store.SavePreset('人员信息', [string[]]@('A', 'B', 'C')) $true 'Overwrite save must return true'
$overwritten = (New-Store $storePath).Load()
Assert-Equal $overwritten.Count 1 'Same-name save must overwrite, not append'
Assert-Equal $overwritten[0].Headers.Length 3 'Overwritten preset must have the new headers'

# 4. 多个预设按保存顺序保留
Assert-Equal $store.SavePreset('订单', [string[]]@('Order', 'Qty')) $true 'Second save must return true'
$ordered = (New-Store $storePath).Load()
Assert-Equal $ordered.Count 2 'Both presets must persist'
Assert-Equal $ordered[0].Name '人员信息' 'Presets must keep save order (first)'
Assert-Equal $ordered[1].Name '订单' 'Presets must keep save order (second)'

# 5. 删除
Assert-Equal $store.DeletePreset('人员信息') $true 'Deleting an existing preset must return true'
Assert-Equal $store.DeletePreset('不存在') $false 'Deleting a missing preset must return false'
Assert-Equal (New-Store $storePath).Load().Count 1 'Deleted preset must be gone from disk'

# 6. 损坏文件降级为空列表
Set-Content -LiteralPath $storePath -Value '{ not valid json !!' -Encoding UTF8
Assert-Equal (New-Store $storePath).Load().Count 0 'Corrupted store file must degrade to an empty list'

# 7. 空白名称拒绝保存
Assert-Equal $store.SavePreset('   ', [string[]]@('X')) $false 'Blank preset name must be rejected'

Remove-Item -Recurse -Force -LiteralPath $tempDir

Write-Output 'HeaderPresetStore tests passed.'
