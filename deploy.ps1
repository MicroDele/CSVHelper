$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'CsvReaderApp\CsvReaderApp.csproj'
$dist = Join-Path $PSScriptRoot 'dist'

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet publish $project -c Release -o $dist
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "已发布到: $dist"
