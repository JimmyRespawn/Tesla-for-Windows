#Requires -Version 7.0
# Reports file names/categories only. Never prints credentials, account data or identities.
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$applicationRoot = Join-Path $repositoryRoot 'Tesla for WinUI/Tesla for WinUI'
$packagingRoot = Join-Path $repositoryRoot 'Tesla for WinUI/Tesla for WinUI (Package)'
$probeRoot = Join-Path $repositoryRoot '.build/repository-privacy-probe'
New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot '.gitignore') -Destination (Join-Path $probeRoot '.gitignore') -Force
& git -C $probeRoot init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Git is required to verify ignore rules.' }
function Get-IgnoredPaths([string[]]$paths) {
    # NUL-delimited input avoids PowerShell's native CRLF pipeline becoming
    # part of a filename, and supports spaces/non-ASCII file names.
    $output = ([string]::Join([char]0, $paths) + [char]0) | & git -C $probeRoot check-ignore --no-index -z --stdin
    if ($LASTEXITCODE -gt 1) { throw 'Ignore rule evaluation failed.' }
    return @($output -split '\x00' | Where-Object { $_ -and $_ -notmatch '^\s*$' })
}

$files = @(Get-ChildItem -LiteralPath $repositoryRoot -Recurse -Force -File | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' })
$relative = @($files | ForEach-Object { $_.FullName.Substring($repositoryRoot.Length + 1).Replace('\','/') })
$ignored = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
@(Get-IgnoredPaths $relative) | ForEach-Object { [void]$ignored.Add($_) }

$privatePaths = @(
    'Tesla for WinUI/Tesla for WinUI/TeslaCredentials.local.json',
    'Tesla for WinUI/Tesla for WinUI/StoreConfiguration.local.json',
    'Tesla for WinUI/Tesla for WinUI (Package)/Package.local.appxmanifest',
    'Tesla for WinUI/Tesla for WinUI (Package)/PackageSigning.local.props',
    'Tesla for WinUI/Tesla for WinUI (Package)/Package.StoreAssociation.xml',
    '.local/TestData.original/me.json', '.local/signing/package.pfx',
    '.build/result.json', '.vs/solution.suo', 'summary.json', 'migration-smoke-results.txt',
    'Tesla for WinUI/Tesla for WinUI/bin/Debug/app.dll',
    'Tesla for WinUI/Tesla for WinUI/obj/project.assets.json',
    'Tesla for WinUI/Tesla for WinUI (Package)/AppPackages/package.msix'
)
$privateIgnored = @(Get-IgnoredPaths $privatePaths)
if ($privateIgnored.Count -ne $privatePaths.Count) { throw 'At least one private path is not ignored.' }
$publicPaths = @(
    'Tesla for WinUI/Tesla for WinUI/TeslaCredentials.example.json',
    'Tesla for WinUI/Tesla for WinUI/StoreConfiguration.example.json',
    'Tesla for WinUI/Tesla for WinUI (Package)/Package.appxmanifest',
    'Tesla for WinUI/Tesla for WinUI (Package)/PackageSigning.example.props',
    'Tesla for WinUI/Tesla for WinUI/Properties/PublishProfiles/win-x64.pubxml',
    'Tesla for WinUI/Tesla for WinUI/Properties/PublishProfiles/win-x86.pubxml',
    'Tesla for WinUI/Tesla for WinUI/Properties/PublishProfiles/win-arm64.pubxml'
)
if (@(Get-IgnoredPaths $publicPaths).Count) { throw 'A portable template or required publish profile is incorrectly ignored.' }
if (Test-Path -LiteralPath (Join-Path $applicationRoot 'Strings/zh-Hans1')) { throw 'Obsolete Chinese resource directory is still present.' }
$publicManifest = [xml](Get-Content -LiteralPath (Join-Path $packagingRoot 'Package.appxmanifest') -Raw)
if ($publicManifest.Package.Identity.Name -ne 'TeslaMurphy.WinUI.Development' -or $publicManifest.Package.Identity.Publisher -ne 'CN=TeslaMurphyDevelopment') { throw 'Public manifest no longer uses the neutral development identity.' }
$credentialExample = Get-Content -LiteralPath (Join-Path $applicationRoot 'TeslaCredentials.example.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($region in $credentialExample.Values) {
    foreach ($key in @('ClientId','ClientSecret','CommandBaseUrl')) {
        if (-not [string]::IsNullOrWhiteSpace([string]$region[$key])) { throw 'Credential example must not contain real configuration.' }
    }
}
$storeExample = Get-Content -LiteralPath (Join-Path $applicationRoot 'StoreConfiguration.example.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($value in $storeExample.Values) { if (-not [string]::IsNullOrWhiteSpace([string]$value)) { throw 'Store example must not contain a product identifier.' } }

# Compare candidate source files with known local secrets without exposing values.
$known = [System.Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
function Add-PrivateValue($value, [string]$category) {
    $text = [string]$value
    if ($text.Length -ge 8 -and $text -notmatch '^0{8}-' -and $text -notmatch '@example\.(com|org|net)$') { $known[$text] = $category }
}
foreach ($local in @('TeslaCredentials.local.json','StoreConfiguration.local.json')) {
    $path = Join-Path $applicationRoot $local
    if (Test-Path -LiteralPath $path) {
        $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        if ($local -like 'Tesla*') {
            foreach ($region in $json.Values) { foreach ($key in @('ClientId','ClientSecret')) { Add-PrivateValue $region[$key] 'local credential' } }
        } else { foreach ($value in $json.Values) { Add-PrivateValue $value 'local Store identifier' } }
    }
}
$localManifest = Join-Path $packagingRoot 'Package.local.appxmanifest'
if (Test-Path -LiteralPath $localManifest) {
    $manifest = [xml](Get-Content -LiteralPath $localManifest -Raw)
    if ($manifest.Package.Identity.Name -ne 'TeslaMurphy.WinUI.Development') { Add-PrivateValue $manifest.Package.Identity.Name 'local package identity' }
    if ($manifest.Package.Identity.Publisher -ne 'CN=TeslaMurphyDevelopment') { Add-PrivateValue $manifest.Package.Identity.Publisher 'local publisher identity' }
    if ($manifest.Package.PhoneIdentity) { Add-PrivateValue $manifest.Package.PhoneIdentity.PhoneProductId 'local phone package identity' }
}
function Read-PrivateFixture($value) {
    if ($value -is [System.Collections.IDictionary]) {
        foreach ($key in $value.Keys) {
            if ($key -match '^(vin|email|vault_uuid|public_key|backseat_token|access_token|refresh_token)$') { Add-PrivateValue $value[$key] 'original API fixture identifier' }
            Read-PrivateFixture $value[$key]
        }
    } elseif ($value -is [System.Collections.IList]) { foreach ($item in $value) { Read-PrivateFixture $item } }
}
$backup = Join-Path $repositoryRoot '.local/TestData.original'
if (Test-Path -LiteralPath $backup) {
    foreach ($file in Get-ChildItem -LiteralPath $backup -File -Filter '*.json') { Read-PrivateFixture (Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -AsHashtable) }
}
$textExtensions = @('.cs','.xaml','.json','.xml','.resw','.resx','.config','.manifest','.md','.html','.css','.js','.ps1','.cjs','.props','.targets','.csproj','.wapproj','.pubxml','.appxmanifest','.slnx','.yml','.yaml','.toml','.txt')
$problems = [System.Collections.Generic.List[string]]::new()
$scanned = 0
for ($index = 0; $index -lt $files.Count; $index++) {
    if ($ignored.Contains($relative[$index]) -or $files[$index].Extension -notin $textExtensions) { continue }
    $text = Get-Content -LiteralPath $files[$index].FullName -Raw
    if ($null -eq $text) { continue }
    $scanned++
    foreach ($entry in $known.GetEnumerator()) {
        if ($text.Contains($entry.Key)) { $problems.Add($relative[$index] + ': ' + $entry.Value); break }
    }
    if ($text -match '(?m)^-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----\s*$') { $problems.Add($relative[$index] + ': private key material') }
    if ($text -match '(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]{20,}\.eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}(?![A-Za-z0-9_-])') { $problems.Add($relative[$index] + ': embedded JWT token') }
}
if ($problems.Count) { $problems | Sort-Object -Unique | Write-Output; throw 'Private values remain in candidate repository files.' }
Write-Output "PASS: $($privatePaths.Count) private path rules, $($publicPaths.Count) public templates/profiles, $scanned candidate text files; no known local private values found."
if (Test-Path -LiteralPath (Join-Path $repositoryRoot '.git')) {
    $tracked = @(& git -C $repositoryRoot ls-files -ci --exclude-standard)
    if ($tracked.Count) { $tracked | Write-Output; throw 'Ignored files are already tracked. Remove them from the index before committing.' }
}
