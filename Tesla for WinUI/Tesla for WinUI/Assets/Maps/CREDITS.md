# Homepage map

## Bundled renderer

- MapLibre GL JS **5.7.1**, BSD 3-Clause; bundled files are in `vendor/`.
- Official source: https://github.com/maplibre/maplibre-gl-js/tree/v5.7.1
- Official package: https://registry.npmjs.org/maplibre-gl/-/maplibre-gl-5.7.1.tgz
- Package SHA-512 (Base64): `iCOQB6W/EGgQx8aU4SyfU5a5/GR2E+ELF92NMsqYfs3x+vnY+8mARmz4gor6XZHCz3tv19mnotVDRlRTMNKyGw==`
- Renderer and included dependency notices: `vendor/LICENSE.txt`.

## Online basemap

OpenFreeMap styles: `https://tiles.openfreemap.org/styles/liberty` (light) and
`https://tiles.openfreemap.org/styles/dark` (dark). Map styles, tiles, glyphs and
sprites are requested directly from `tiles.openfreemap.org`; no map credentials
are needed. The renderer is local, but the basemap requires an Internet connection.
The dark style's colors are adjusted locally to improve road and label readability.

Map attribution stays visible: OpenFreeMap, © OpenMapTiles and © OpenStreetMap.
OpenStreetMap data is licensed under ODbL; using the free public map host does not
remove data attribution requirements. The public host has no service-level guarantee.

- Integration: https://openfreemap.org/quick_start/
- Public host terms: https://openfreemap.org/terms/
- Data attribution: https://www.openstreetmap.org/copyright

The map receives vehicle/charging-site coordinates from the app and draws markers
locally. Marker names and Tesla tokens are not included in basemap requests.
Requests for visible map tiles necessarily reveal the viewed geographic area to
the map host. Address search and route computation are not included in the basemap.

## Optional external route viewer

When the user selects a station and clicks “View route in Amap”, the app opens
`https://uri.amap.com/navigation` in the default browser. The link contains the
vehicle and station coordinates (converted from WGS84 to GCJ-02), display names,
and driving-route parameters. It contains no Tesla credentials or map API key.
The Amap website computes/displays the route and may require login or human verification.

- Route URI: https://lbs.amap.com/api/uri-api/guide/travel/route
- URI coordinates: https://lbs.amap.com/api/uri-api/gettingstarted
