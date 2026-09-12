# Tesla for Windows

Since Tesla decide charge for API usage. So this project will be opensourced now.

This app is used for overviewing your Tesla car's information like battery percentage, charge state, fan state, nearby charging station, climate info, schedule info, driver's info and latest release notes for your car.

Climate control supports starting and stopping preconditioning. Vehicles requiring signed commands need a configured Tesla Vehicle Command Proxy and a paired virtual key. Other vehicle controls have not been implemented yet.

## Screenshots
<img width="1919" alt="4" src="https://github.com/user-attachments/assets/c7c316dd-1dbf-4aea-8bf8-9fb4c0164919" />
<img width="1919" alt="3" src="https://github.com/user-attachments/assets/6db9e534-c2e8-413e-879f-9a926f0014af" />


## How to compile

IDE: Visual studio 2022 with UWP sdk installed.

Minimum OS version: Windows 10 1903,18362 or higher

### Local package publisher

The tracked `TeslaMurphy/Package.appxmanifest` keeps `Publisher="CN="` as a
placeholder. For local deployment:

1. Copy `TeslaMurphy/Package.appxmanifest` to
   `TeslaMurphy/Package.local.appxmanifest`.
2. In the local copy, change the `Identity` element's `Publisher` to your actual
   publisher, for example `CN=YourName`. Use the full subject of your signing
   certificate, and configure the matching certificate for package signing.
3. Reload the project in Visual Studio after creating or removing the local
   manifest, then rebuild and deploy.

The project automatically selects the local manifest when it exists. Git ignores
this file, so your personal publisher is not committed. Edit shared manifest
settings in `Package.appxmanifest` and keep those changes in sync with your local
copy. Remove the local copy to use the tracked manifest again. A fresh checkout
or CI build must supply a valid publisher and signing setup before deployment;
the tracked `CN=` value is only a placeholder.

## What you need to prepare to get car online service work

### Step 1 

Go to https://developer.tesla.com/docs/fleet-api to create Tesla account

### Step 2

Apply for API key and register/activate your partner account one time. There is code in the project, but there is no button to call it. Do your own magic to activate it.s

### Step 3

Copy `TeslaMurphy/TeslaCredentials.example.json` to
`TeslaMurphy/TeslaCredentials.local.json` and fill in `ClientId` and `ClientSecret`
for each region you use (`NA`, `EU`, `CN`). Reload the project after creating the
file, then rebuild. The local file is ignored by Git; only the empty example is
committed. Without local credentials, the app can run in demo mode and displays
a configuration message when login is attempted.

Region URLs and credential selection are centralized in
`Services/TeslaConfiguration.cs`. `Services/HttpService.cs` shares one HTTP client
for Fleet API and OAuth requests; `TeslaFleetServices` handles authorization and
token exchange. Existing vehicle endpoint methods use this shared transport.

Local credentials are embedded into the compiled app for local development.
Git exclusion prevents source commits, but does not hide secrets inside a
distributed app or build artifact. For public distribution, keep the client
secret and token exchange on a backend you control.

e.g. Api key in US/EU is different from Mainland China

## What you need to get map service work

Go to https://azure.microsoft.com/en-us/products/azure-maps/ to apply Bing maps api if you want to get the map control working in release version.

The service is okay without token in debug mode.

Search `MapServiceToken` and replace it with your own token.

## More info

You are free to modify and distribute it to the store. Just follow the GPL3.0 license.

Any activities that violates the privacy policy including store the users' data into your own server. Or monitor users' data will not be allowed.

## Lock, climate and schedule control

The climate toolbar button starts (`auto_conditioning_start`) or stops
(`auto_conditioning_stop`) climate preconditioning using the selected vehicle VIN.
Only a response with `response.result: true` is treated as success. An expired
access token triggers one refresh and retry. Failed requests restore the button
state, and demo mode never sends commands.

For vehicles requiring signed commands, deploy the official
[Tesla Vehicle Command Proxy](https://github.com/teslamotors/vehicle-command#http-proxy)
with your application's private key and pair the corresponding virtual key with
the vehicle. Add `"CommandBaseUrl": "https://your-command-proxy.example"` alongside
`ClientId` and `ClientSecret` in the appropriate region of
`TeslaCredentials.local.json`, then rebuild. Configure the proxy for that Tesla
region. Use a trusted HTTPS certificate; the app does not bypass TLS validation.
Only use a proxy you control: vehicle command requests send it the access token.
Keep the signing private key on the proxy server, not in the UWP package.

An omitted or empty `CommandBaseUrl` sends commands directly to Fleet API, for
vehicles that support unsigned commands. Reading vehicle data, waking the vehicle
and OAuth token requests continue using the regional Fleet API/auth endpoints.
This setting does not deploy a proxy or pair a virtual key automatically. The
vehicle must be online; if it is asleep, wake it and retry. See Tesla's
[command requirements](https://developer.tesla.com/docs/fleet-api/endpoints/vehicle-commands).