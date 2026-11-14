param(
    [Parameter(Mandatory)][string]$Binary,
    [Parameter(Mandatory)][string]$Url,
    [string]$PrivateKeyFile = '',
    [string]$ProtectedKeyFile = (Join-Path $env:LOCALAPPDATA 'NexusPublisher/update-signing-key.dpapi'),
    [string]$Version = '',
    [string]$Output = 'release.json'
)
$ErrorActionPreference = 'Stop'
if (-not $Url.StartsWith('https://')) { throw 'An HTTPS release URL is required' }
if (-not $Version) { $Version = ([xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw)).Project.PropertyGroup.Version }
$digest = (Get-FileHash -LiteralPath $Binary -Algorithm SHA256).Hash.ToLowerInvariant()
$payload = [ordered]@{ Version = $Version; Url = $Url; Sha256 = $digest } | ConvertTo-Json -Compress
$rsa = [System.Security.Cryptography.RSA]::Create()
$secret = $null
try {
    if ($PrivateKeyFile) { $rsa.ImportFromPem([System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $PrivateKeyFile))) }
    else {
        # DPAPI restricts decryption to the Windows user that created the publisher key.
        $secret = [System.Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $ProtectedKeyFile)), $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
        $read = 0; $rsa.ImportPkcs8PrivateKey($secret, [ref]$read)
        if ($read -ne $secret.Length) { throw 'Invalid protected signing key' }
    }
    $signature = $rsa.SignData([System.Text.Encoding]::UTF8.GetBytes($payload), [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pss)
    [ordered]@{ Payload = $payload; Signature = [Convert]::ToBase64String($signature) } | ConvertTo-Json | Set-Content -LiteralPath $Output -Encoding utf8NoBOM
} finally { if ($secret) { [System.Security.Cryptography.CryptographicOperations]::ZeroMemory($secret) }; $rsa.Dispose() }
