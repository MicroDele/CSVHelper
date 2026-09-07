param(
    [string]$Version = "",
    [string]$RepoUrl = "https://github.com/MicroDele/CSVHelper",
    [string]$Token = "",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    # MSI by default: standard Windows Installer package, much lower false-positive
    # rate than Setup.exe. Use -Msi:$false to fall back to the one-click installer.
    [bool]$Msi = $true,
    [switch]$SelfContained,
    [switch]$Upload
)

$ErrorActionPreference = 'Stop'

# Load .env (KEY=VALUE lines) into the process environment if present, so secrets
# like GH_TOKEN stay out of the script and out of source control.
$envFile = Join-Path $PSScriptRoot '.env'
if (Test-Path -LiteralPath $envFile) {
    Get-Content -LiteralPath $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith('#') -and $line -match '^([^=]+)=(.*)$') {
            $name  = $Matches[1].Trim()
            $value = $Matches[2].Trim().Trim('"').Trim("'")
            [Environment]::SetEnvironmentVariable($name, $value, 'Process')
        }
    }
}

$project    = Join-Path $PSScriptRoot 'CsvReaderApp\CsvReaderApp.csproj'
$publishDir = Join-Path $PSScriptRoot 'publish'
$releaseDir = Join-Path $PSScriptRoot 'Releases'

# Version: use -Version if given, otherwise read <Version> from csproj.
if (-not $Version) {
    $csproj = Get-Content -Raw -LiteralPath $project
    if ($csproj -match '<Version>([^<]+)</Version>') {
        $Version = $Matches[1]
    }
    else {
        throw 'Unable to read <Version> from csproj. Pass -Version explicitly.'
    }
}

Write-Host "Version: $Version"

# 1. Publish. Framework-dependent by default (fast, no runtime download);
#    pass -SelfContained to bundle the .NET runtime (target machine needs no .NET installed).
Write-Host 'Publishing...'
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if ($SelfContained) {
    dotnet publish $project -c $Configuration -r $Runtime --self-contained -o $publishDir
}
else {
    dotnet publish $project -c $Configuration -o $publishDir
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 2. Pack: generates Setup.exe installer + full/delta update packages.
#    With -Msi (default on) WiX also produces a .msi next to Setup.exe, which
#    has a much lower SmartScreen / AV false-positive rate than the 7z-SFX Setup.exe.
Write-Host 'Packing...'
$packArgs = @('pack', '-u', 'CSVHelper', '-v', $Version, '-p', $publishDir, '-o', $releaseDir, '-r', $Runtime, '--mainExe', 'CSVHelper.exe', '--framework', 'net10.0-x64-desktop')
if ($Msi) {
    $packArgs += '--msi'
    # PerUser install keeps it under %LocalAppData% (no admin needed), matching
    # the Setup.exe default. Use 'PerMachine' for Program Files / HKLM installs.
    $packArgs += @('--instLocation', 'PerUser')
}
vpk @packArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 3. Upload to GitHub Releases (requires a token with repo/contents scope).
#    Upload is opt-in: pass -Upload to publish. By default the script only builds
#    and packs locally so an accidental run can't publish a release.
if ($Upload) {
    # Resolve token: -Token param > GH_TOKEN/GITHUB_TOKEN env vars > GitHub CLI (gh auth token).
    if (-not $Token) { $Token = $env:GH_TOKEN }
    if (-not $Token) { $Token = $env:GITHUB_TOKEN }
    if (-not $Token) {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if ($gh) {
            $ghToken = & gh auth token 2>$null
            if ($LASTEXITCODE -eq 0 -and $ghToken) {
                $Token = $ghToken
                Write-Host 'Using token from GitHub CLI (gh auth token).'
            }
        }
    }
    if (-not $Token) {
        throw 'Missing GitHub token. Run "gh auth login" (or set GH_TOKEN / pass -Token).'
    }
    Write-Host 'Uploading to GitHub Releases...'
    vpk upload github -o $releaseDir --repoUrl $RepoUrl --token $Token --publish
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($Upload) {
    Write-Host "Done. Published $Version and packages are in: $releaseDir"
} else {
    Write-Host "Done. Local build only. Add -Upload to publish to GitHub. Packages in: $releaseDir"
}
