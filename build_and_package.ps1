param(
    [string]$Version = '1.0.6'
)

$ErrorActionPreference = 'Stop'

$publishDir = Join-Path (Get-Location) 'publish'
$zipName = "InventariAgent_v$Version.zip"
$zipPath = Join-Path (Get-Location) $zipName

Write-Output "Publish dir: $publishDir"
Write-Output "ZIP path: $zipPath"

if (-not (Test-Path $publishDir)) {
    Write-Error "Publish directory not found: $publishDir"
    exit 1
}

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToUpper()
$shaFile = "$zipPath.sha256"
$hash | Out-File -FilePath $shaFile -NoNewline -Encoding ASCII

Write-Output "Created: $zipPath"
Write-Output "SHA256: $hash"
Write-Output "SHA file: $shaFile"
