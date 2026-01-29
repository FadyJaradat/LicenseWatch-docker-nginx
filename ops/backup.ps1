param(
    [string]$ContainerName = $env:LICENSEWATCH_CONTAINER
)

if ([string]::IsNullOrWhiteSpace($ContainerName)) {
    $ContainerName = "licensewatch-web"
}

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$BackupDir = Join-Path $Root "backups"
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

$running = docker ps --format "{{.Names}}" | Select-String -Pattern "^$ContainerName$"
if (-not $running) {
    Write-Error "Container '$ContainerName' is not running. Start it first (docker compose --profile dev up -d)."
    exit 1
}

$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$archive = "licensewatch-backup-$stamp.tar.gz"

docker exec $ContainerName sh -c "tar -czf /tmp/$archive -C /app App_Data --exclude=App_Data/imports/tmp --exclude=App_Data/backups"
docker cp "$ContainerName:/tmp/$archive" (Join-Path $BackupDir $archive)
docker exec $ContainerName rm -f "/tmp/$archive"

Write-Host "Backup saved to $BackupDir\$archive"
