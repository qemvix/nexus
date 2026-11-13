param([Parameter(Mandatory)][string]$Binary, [Parameter(Mandatory)][string]$Url, [Parameter(Mandatory)][string]$PrivateKeyFile, [string]$Version = '0.2.0', [string]$Output = 'release.json')
$ErrorActionPreference = 'Stop'
# PowerShell 7+ is required for RSA.ImportFromPem. Keep private keys outside this repository.
if (-not $Url.StartsWith('https://')) { throw 'An HTTPS release URL is required' }
$digest = (Get-FileHash -LiteralPath $Binary -Algorithm SHA256).Hash.ToLowerInvariant()
$payload = [ordered]@{ Version = $Version; Url = $Url; Sha256 = $digest } | ConvertTo-Json -Compress
$rsa = [System.Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem([System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $PrivateKeyFile)))
    $signature = $rsa.SignData([System.Text.Encoding]::UTF8.GetBytes($payload), [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pss)
    [ordered]@{ Payload = $payload; Signature = [Convert]::ToBase64String($signature) } | ConvertTo-Json | Set-Content -LiteralPath $Output -Encoding utf8NoBOM
} finally { $rsa.Dispose() }
