# Tesla for WinUI

A WinUI 3 desktop client migrated from UWP, built with .NET 8, Windows App SDK,
and HelixToolkit. The project retains the original page and folder structure
and adds a native 3D view for vehicle status, real vehicle controls, and local
driving simulation.

## Features

- Vehicle information and controls using the existing Tesla API integration.
- A native Model 3 scene rendered with HelixToolkit / DirectX, with body hotspots,
  interior and showroom views, and a HUD that can be hidden.
- Local driving simulation with traffic, turn signals, lane changes, brake lights,
  body motion, and weather and road options.
- A homepage map with vehicle and nearby charging station markers, plus an option
  to open driving directions in Amap.
- English and Simplified Chinese resources, with light and dark scenes that follow
  the application theme.

Local simulation controls only animate the model. Real vehicle controls require a
configured Tesla login and, where applicable, a vehicle command proxy. See
[MODEL3-3D.md](MODEL3-3D.md) for supported actions, API limitations, and validation notes.

## Local setup

Open `Tesla for WinUI.slnx` in Visual Studio, select **x64 / Debug**, and set
**Tesla for WinUI (Package)** as the startup project.

After obtaining the source, run the following command from the repository root:

```powershell
pwsh -File Tools/Prepare-LocalConfig.ps1
```

The script creates missing local configuration files without overwriting existing
ones. Configure these files before signing in or using store features.

| Public file (commit to Git) | Local file (ignored by Git) | Purpose |
| --- | --- | --- |
| `Tesla for WinUI/Tesla for WinUI/TeslaCredentials.example.json` | `TeslaCredentials.local.json` in the same folder | Tesla OAuth credentials, command proxy URL, and optional coordinate conversion |
| `Tesla for WinUI/Tesla for WinUI/StoreConfiguration.example.json` | `StoreConfiguration.local.json` in the same folder | Store subscription and application product IDs |
| `Tesla for WinUI/Tesla for WinUI (Package)/Package.appxmanifest` | `Package.local.appxmanifest` in the same folder | Package identity, publisher, version, and other packaging settings |
| `Tesla for WinUI/Tesla for WinUI (Package)/PackageSigning.example.props` | `PackageSigning.local.props` in the same folder | Optional local signing settings |

Builds use the local manifest when available and fall back to the public development
manifest otherwise. The public manifest uses a generic development identity,
separate from the production store identity. Copy and configure the signing
template manually if you need package signing; the setup script does not create it.

Enter actual credentials only in `*.local.json` files and rebuild after changing
them. Login tokens, vehicle caches, and user preferences use Windows application
local storage and are not committed as source files.

Public demo data in `Assets/TestData` has been sanitized. Original local data
backups, when present, live in the ignored `.local/TestData.original/` folder and
are not included as package resources.

## Before committing

Run the privacy check in PowerShell 7:

```powershell
pwsh -File Tools/Test-RepositoryPrivacy.ps1
```

The script checks that local configuration, signing files, store associations,
build outputs, and API data backups are ignored by Git. It also scans candidate
text files for known private values from local configuration. Reports contain
file names and issue categories without printing those values. If a Git repository
has already been initialized, the script also checks for ignored files that are
still tracked. Run it again after adding credentials or API response samples.

`.gitignore` keeps source files, public configuration templates, and the three
portable publish profiles. It excludes `.vs`, `.build`, `.local`, `bin`, `obj`,
`AppPackages`, certificates and private keys, user configuration, and temporary
diagnostic output.

Local builds embed local configuration in the application. Generated DLL, EXE,
and MSIX files are excluded from source control.

## Public build

Run this command in a Visual Studio Developer PowerShell terminal:

```powershell
MSBuild.exe 'Tesla for WinUI.slnx' /t:Rebuild /restore /p:Platform=x64 /p:Configuration=Debug /p:UseLocalConfiguration=false /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=true /p:AppxBundle=Never
```

`UseLocalConfiguration=false` excludes local credentials, store configuration,
the private manifest, and local signing settings. The resulting public demo
package does not include machine-local credentials; real vehicle login and store
product features require separate configuration.

For everyday local debugging, omit this property to keep using your existing
local files.

## Localization and 3D assets

Maintain Simplified Chinese resources in `Strings/zh-Hans` and English resources
in `Strings/en-US`. The duplicate `zh-Hans1` folder has been merged and removed.

Migration notes are in [MIGRATION.md](MIGRATION.md). Native 3D features and
validation notes are in [MODEL3-3D.md](MODEL3-3D.md).

Model sources and licenses are documented in
[Model 3 credits](Tesla%20for%20WinUI/Tesla%20for%20WinUI/Assets/Model3/CREDITS.md)
and [traffic asset credits](Tesla%20for%20WinUI/Tesla%20for%20WinUI/Assets/Traffic/CREDITS.md).
The previous Three.js / WebView2 3D page is archived and hidden from navigation;
see the [archive notes](Tesla%20for%20WinUI/Tesla%20for%20WinUI/Views/Archive/README.md).

## Homepage map

The homepage uses bundled MapLibre GL JS and the OpenFreeMap global basemap.
No map API key or subscription is required. It supports panning, zooming, the
application's light and dark themes, vehicle and nearby charging station markers,
station selection, and returning to the vehicle position.

Vehicle and charging station data still come from the Tesla API. The basemap
requires an internet connection and provides a retry option if loading fails.
The application does not perform address search or route calculation; the previous
in-app search and routing controls have been removed.

After selecting a nearby charging station, click **View route in Amap** to open
driving directions in the default browser, using the vehicle position as the
origin and the station as the destination. This does not require a developer key.
If the vehicle position is unavailable, refresh the map first. Amap may require
login or manual verification; its website provides the route results.

The origin and destination sent to Amap are converted from the application's
WGS84 coordinates to GCJ-02. This does not change the markers displayed in the
application.

The basemap uses WGS84 coordinates. Tesla coordinates are not converted by
default. If a China-region endpoint is confirmed to return GCJ-02 coordinates
and its markers are offset, set the corresponding option in the `CN` section of
`TeslaCredentials.local.json` to `Gcj02ToWgs84`:

- `VehicleCoordinateConversion` for the vehicle position.
- `ChargingCoordinateConversion` for charging stations.

These options are independent to avoid applying a correction to the wrong data
source or converting coordinates twice.

See [map credits](Tesla%20for%20WinUI/Tesla%20for%20WinUI/Assets/Maps/CREDITS.md)
for library versions, licenses, data attribution, and network sources.
