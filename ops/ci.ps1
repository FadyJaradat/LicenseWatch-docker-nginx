param(
    [switch]$Docker
)

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

dotnet restore
dotnet build LicenseWatch.slnx -c Release
dotnet test LicenseWatch.slnx -c Release

if ($Docker) {
    docker build -t licensewatch:ci .
}

Write-Host "CI checks completed."
