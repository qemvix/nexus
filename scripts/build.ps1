param([switch]$Publish, [switch]$Integration, [switch]$ScrollInputTests)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    dotnet restore Nexus.sln
    if ($LASTEXITCODE) { throw 'Restore failed' }
    dotnet build Nexus.sln -c Release --no-restore -warnaserror
    if ($LASTEXITCODE) { throw 'Build failed' }
    dotnet run --project tests/Nexus.Tests -c Release --no-build
    if ($LASTEXITCODE) { throw 'Tests failed' }
    if ($Integration) {
        dotnet run --project tests/Nexus.Tests -c Release --no-build -- --integration
        if ($LASTEXITCODE) { throw 'Native integration checks failed' }
    }
    if ($ScrollInputTests) {
        dotnet run --project tests/Nexus.Tests -c Release --no-build -- --glide-integration
        if ($LASTEXITCODE) { throw 'Glide native wheel input checks failed' }
    }
    if ($Publish) {
        dotnet publish apps/desktop/Nexus.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o artifacts/publish
        if ($LASTEXITCODE) { throw 'Publish failed' }
        New-Item -ItemType Directory -Path artifacts/release -Force | Out-Null
        Copy-Item -LiteralPath artifacts/publish/Nexus.exe -Destination artifacts/release/Nexus-Setup.exe -Force
        Copy-Item -LiteralPath artifacts/publish/Nexus.exe -Destination artifacts/release/Nexus.exe -Force
        Compress-Archive -LiteralPath artifacts/release/Nexus.exe -DestinationPath artifacts/release/Nexus-0.2.0-win-x64-portable.zip -Force
        Get-ChildItem -LiteralPath artifacts/release -File | Where-Object Name -ne SHA256SUMS.txt | ForEach-Object { $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $($_.Name)" } | Set-Content -LiteralPath artifacts/release/SHA256SUMS.txt
    }
} finally { Pop-Location }
