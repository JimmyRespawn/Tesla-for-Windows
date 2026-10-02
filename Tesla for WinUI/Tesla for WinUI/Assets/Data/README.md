# Range reference catalog

`tesla-epa-ranges.json` contains 185 Tesla records (model years 2012–2026) extracted on 2026-09-16 from the US Department of Energy / EPA vehicle dataset:
https://www.fueleconomy.gov/feg/epadata/vehicles.csv.zip
Data dictionary: https://www.fueleconomy.gov/feg/ws/

Fields retained: year, model, range (miles), id. Every record links to its EPA vehicle page through epaId. These are certification reference ranges, NOT measured original battery capacities or guaranteed new-vehicle dashboard ranges.

## Automatic matching

RangePresetService requires a supported US-built VIN, matching vehicle model, US charge port, explicitly non-European / left-hand-drive configuration, model year, supported trim badge, and recognizable wheel size. Wheel-specific records must match the wheel size. Only one candidate record may remain. Unspecified wheel options in EPA rows remain certification references, not exact vehicle measurements.

Numeric trim badges are reused across model years. Matching uses model + year + candidate badge aliases; it never selects the closest range, first candidate, or average. Current vehicle range is not used to choose a baseline. Legacy numeric S/X badges are restricted to 2012–2018. Unknown modern S/X and Cybertruck badges remain unmatched even when the catalog contains their marketing-name records.

Chinese configurations use a separate `tesla-cn-ranges.json` catalog (eight configuration-family references). These are owner-reported displayed ranges, NOT Tesla official specifications. Exact configuration applicability and adjacent-year grouping are conservative inferences; see each row's note. Require LRW VIN + GB charge port + matching year/model/badge/wheel code and, for LFP rows, VIN chemistry code F. Exported Shanghai cars do not match. European configurations remain unmatched.

CN coverage: 2021 Model 3 LFP (420–439 km interval); 2022–2023 classic Model 3 RWD (439 km); 2023–2024 Highland RWD (422 km); 2024 Highland LR (550 km extrapolated from an owner-reported 80% reading); 2021–2024 classic Y RWD (435 km); 2022–2023 Y LR (520–528 km); 2025 Juniper RWD (401–435 km pooled interval); 2025 Juniper LR (520–541 km pooled interval). These are only applicable to the wheel codes enumerated in each row. A point reference remains approximate, not a guarantee of the individual vehicle's original range.

Intervals intentionally cover reported pack/firmware variation, are not statistical confidence intervals, and must not be collapsed to a midpoint. The UI computes [current/max, current/min] × 100. 2021 Model 3's upper bound includes the adjacent 60-kWh generation as a conservative inference because the API does not supply a production month/pack identifier. It is not a sourced assertion that every 2021 vehicle shipped with either exact value.

No universal coverage is claimed: China-market imported S/X, Performance variants without researched references, 2025 Model 3 and 2026/other unknown versions remain unmatched. No CLTC/WLTP conversion or US fallback is used.

VIN references:
- https://service.tesla.com/docs/ModelY/ServiceManual/en-us/GUID-0C797294-574D-4EE4-8017-C339A7D58411.html
- https://service.tesla.com/docs/ModelS/ServiceManual/en-us/GUID-BED77626-E575-4DB7-8C1F-CFA600EAA082.html

Tesla states displayed range uses rated efficiency:
https://www.tesla.cn/support/range

## User values

Explicit user edits remain VIN-scoped local preferences. Automatically selected EPA values are not saved as user measurements. Automatic results are labeled "Estimated range vs. EPA reference", not battery health. Saved original displayed ranges retain the existing estimated range retention label.

## Validation

Run `pwsh -NoProfile -File tests/RangePresetChecks.ps1` from the repository root. Tests use the bundled catalog and synthetic vehicle configurations, with no account or vehicle access.
