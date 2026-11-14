param([string]$Repository = $env:GITHUB_REPOSITORY)
$ErrorActionPreference='Stop'
if($Repository -ne 'qemvix/nexus'){throw 'Desktop releases belong in qemvix/nexus'}
$root=Split-Path -Parent $PSScriptRoot
$version=([xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$tag='desktop-v'+$version
$existingJson=gh release view $tag --repo $Repository --json isDraft 2>$null
if($LASTEXITCODE -eq 0){
    $existing=$existingJson | ConvertFrom-Json
    if(-not $existing.isDraft){Write-Output 'Published release already exists; immutable assets were left unchanged.';exit 0}
} else {
    gh release create $tag --repo $Repository --draft --target $env:GITHUB_SHA --title "Nexus $version" --notes-file (Join-Path $root ('docs/releases/'+$version+'.md'))
    if($LASTEXITCODE){throw 'Could not create release draft'}
}
$releaseDir=Join-Path $root 'artifacts/release'
$names=@('Nexus-Setup.exe',('Nexus-'+$version+'-win-x64-portable.zip'),'SHA256SUMS.txt','Nexus-LICENSE.txt')
foreach($name in $names){
    gh release upload $tag (Join-Path $releaseDir $name) --repo $Repository --clobber
    if($LASTEXITCODE){throw ('Release upload failed: '+$name)}
}
$remote=gh api ('repos/'+$Repository+'/releases/tags/'+$tag) | ConvertFrom-Json
if($LASTEXITCODE){throw 'Could not verify release draft'}
if(@($remote.assets).Count -ne 4){throw 'Unexpected release assets'}
$files=@{}
foreach($name in $names){
    $local=Get-Item -LiteralPath (Join-Path $releaseDir $name)
    $digest=(Get-FileHash -LiteralPath $local.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $asset=$remote.assets | Where-Object name -eq $name
    if(-not $asset -or $asset.size -ne $local.Length -or $asset.digest -ne ('sha256:'+$digest)){throw ('Uploaded asset mismatch: '+$name)}
    $files[$name]=@{bytes=$local.Length;sha256=$digest;url=$asset.browser_download_url}
}
@{version=$version;tag=$tag;repository=$Repository;releaseId=$remote.id;files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'artifacts/release-metadata.json') -Encoding utf8NoBOM
Write-Output ('Verified release draft: '+$remote.html_url)
