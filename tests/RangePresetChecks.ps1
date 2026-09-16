# Offline checks; does not read account credentials or contact vehicles.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$refs = @(Get-ChildItem "$PSHOME/ref/*.dll" | ForEach-Object FullName) + @([Newtonsoft.Json.Linq.JObject].Assembly.Location)
Add-Type -Path @((Join-Path $repoPath 'TeslaMurphy/Services/RangePresetService.cs'), (Join-Path $PSScriptRoot 'RangePresetChecks.cs')) -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701'
[TeslaMurphy.Services.RangePresetChecks]::Run((Get-Content (Join-Path $repoPath 'TeslaMurphy/Assets/Data/tesla-epa-ranges.json') -Raw), (Get-Content (Join-Path $repoPath 'TeslaMurphy/Assets/Data/tesla-cn-ranges.json') -Raw), (Get-Content (Join-Path $repoPath 'TeslaMurphy/Assets/TestData/vehicledata.json') -Raw))
