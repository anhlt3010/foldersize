param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:TEMP = "$projectRoot/.work/tmp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$projectRoot/.work/cli"
$env:NUGET_PACKAGES = "$projectRoot/.work/packages"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force $env:TEMP,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES | Out-Null
$dotnetExe = "$projectRoot/.tools/dotnet/dotnet.exe"
if (!(Test-Path $dotnetExe)) { $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $projectRoot
try {
    & $dotnetExe test FolderSize.sln -c Release
    if ($LASTEXITCODE) { throw 'Tests failed' }
    if ($Publish) {
        & $dotnetExe publish src/FolderSize.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/FolderSize-win-x64
        if ($LASTEXITCODE) { throw 'Publish failed' }
        Compress-Archive artifacts/FolderSize-win-x64/* artifacts/FolderSize-win-x64.zip -Force
    }
} finally { Pop-Location }
