param(
    [Parameter(Mandatory = $true)]
    [string]$Archive,
    [switch]$Force,
    [switch]$NoStop
)

$ArchivePath = Resolve-Path $Archive -ErrorAction Stop
$ArchiveName = Split-Path $ArchivePath -Leaf
$ArchiveDir = Split-Path $ArchivePath -Parent

$entries = tar -tzf $ArchivePath 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Archive is not a valid tar.gz file."
    exit 1
}

foreach ($entry in $entries) {
    if ($entry.StartsWith("/") -or $entry.Contains("..") -or -not $entry.StartsWith("App_Data")) {
        Write-Error "Archive contents are unsafe or unexpected. Restore aborted."
        exit 1
    }
}

if (-not $Force) {
    $confirm = Read-Host "This will overwrite App_Data. Type RESTORE to continue"
    if ($confirm -ne "RESTORE") {
        Write-Host "Restore cancelled."
        exit 1
    }
}

if (-not $NoStop) {
    docker compose --profile dev --profile prod down
}

$volumeName = $env:LICENSEWATCH_VOLUME
if ([string]::IsNullOrWhiteSpace($volumeName)) {
    $volumeName = docker volume ls --filter label=com.docker.compose.volume=identity_data --format "{{.Name}}" | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($volumeName)) {
    Write-Error "Could not locate the App_Data volume. Set LICENSEWATCH_VOLUME to override."
    exit 1
}

docker run --rm -v "$volumeName:/data" -v "$ArchiveDir:/backup" alpine sh -c "rm -rf /data/* /data/.[!.]* /data/..?*; tar -xzf /backup/$ArchiveName -C /data --strip-components=1 App_Data"

Write-Host "Restore complete. Start the app with: docker compose --profile dev up -d"
