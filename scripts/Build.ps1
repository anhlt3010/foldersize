param([switch]$Publish, [string]$OutputDirectory)
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
        $projectXml = [xml](Get-Content src/FolderSize.App/FolderSize.App.csproj -Raw)
        $version = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
        $packageDirectory = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot "artifacts/FolderSize-$version-win-x64" }
        if (Test-Path -LiteralPath $packageDirectory) {
            $unexpected = @(Get-ChildItem -LiteralPath $packageDirectory -Force | Where-Object { $_.PSIsContainer -or $_.Name -notin @('FolderSize.App.exe','LICENSE') })
            if ($unexpected.Count) { throw 'Output contains other files. Choose a fresh -OutputDirectory; existing data will not be removed.' }
        }
        $stagingDirectory = Join-Path $projectRoot ('.work/publish-' + [Guid]::NewGuid().ToString('N'))
        & $dotnetExe publish src/FolderSize.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false -o $stagingDirectory
        if ($LASTEXITCODE) { throw 'Publish failed' }
        $packageFiles = @(Get-ChildItem -LiteralPath $stagingDirectory -Force)
        if ($packageFiles.Count -ne 2 -or @($packageFiles | Where-Object { $_.PSIsContainer -or $_.Name -notin @('FolderSize.App.exe','LICENSE') }).Count) { throw 'Unexpected publish contents; release must contain only EXE and LICENSE.' }
        New-Item -ItemType Directory -Force $packageDirectory | Out-Null
        foreach ($packageFile in $packageFiles) { Copy-Item -LiteralPath $packageFile.FullName -Destination $packageDirectory -Force }
        Compress-Archive -Path (Join-Path $stagingDirectory '*') -DestinationPath "$packageDirectory.zip" -Force
        Write-Output "Release: $packageDirectory"
        Write-Output "ZIP: $packageDirectory.zip"
    }
} finally { Pop-Location }
