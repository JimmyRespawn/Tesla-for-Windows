$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$applicationRoot = Join-Path $repositoryRoot 'Tesla for WinUI/Tesla for WinUI'
$packagingRoot = Join-Path $repositoryRoot 'Tesla for WinUI/Tesla for WinUI (Package)'

foreach ($name in @('TeslaCredentials','StoreConfiguration')) {
    $target = Join-Path $applicationRoot "$name.local.json"
    if (-not (Test-Path -LiteralPath $target)) {
        Copy-Item -LiteralPath (Join-Path $applicationRoot "$name.example.json") -Destination $target
        Write-Output "Created $name.local.json; fill in your own configuration."
    } else { Write-Output "$name.local.json already exists; preserved." }
}
$manifest = Join-Path $packagingRoot 'Package.local.appxmanifest'
if (-not (Test-Path -LiteralPath $manifest)) {
    Copy-Item -LiteralPath (Join-Path $packagingRoot 'Package.appxmanifest') -Destination $manifest
    Write-Output 'Created Package.local.appxmanifest; configure your package identity locally.'
} else { Write-Output 'Package.local.appxmanifest already exists; preserved.' }
