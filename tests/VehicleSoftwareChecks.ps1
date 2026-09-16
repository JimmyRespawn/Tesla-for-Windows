# Run with PowerShell 7. Uses only fake HTTP responses; no vehicle/account access.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$httpPath = Join-Path ([System.IO.Path]::GetTempPath()) ("tesla-driver-http-" + [Guid]::NewGuid().ToString() + ".cs")
try {
    $http = (Get-Content (Join-Path $repoPath 'TeslaMurphy/Services/HttpService.cs') -Raw).Replace(
        'new HttpClient()', 'new HttpClient(new FakeDriverHandler())')
    Set-Content $httpPath $http
    $refs = @(Get-ChildItem "$PSHOME/ref/*.dll" | ForEach-Object FullName) + @([Newtonsoft.Json.Linq.JObject].Assembly.Location)
    Add-Type -Path @($httpPath, (Join-Path $PSScriptRoot 'DriverManagementChecks.cs'),
        (Join-Path $repoPath 'TeslaMurphy/Services/DriverManagementService.cs'), (Join-Path $repoPath 'TeslaMurphy/Services/VehicleSoftwareService.cs'), (Join-Path $PSScriptRoot 'VehicleSoftwareChecks.cs')) -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701'
    [TeslaMurphy.Services.SoftwareChecks]::Run().GetAwaiter().GetResult()
}
finally { Remove-Item -LiteralPath $httpPath -ErrorAction SilentlyContinue }


