# Tesla for Windows

Since Tesla decide charge for API usage. So this project will be opensourced now.

This app is used for overviewing your Tesla car's information like battery percentage, charge state, fan state, nearby charging station, climate info, schedule info, driver's info and latest release notes for your car.

The only control feature works is turning of the AC in your car. The other controls have not been implemented yet. To use control feature, you need to write code for end-to-end encription.

## Screen shots
<img width="1919" alt="4" src="https://github.com/user-attachments/assets/c7c316dd-1dbf-4aea-8bf8-9fb4c0164919" />
<img width="1919" alt="3" src="https://github.com/user-attachments/assets/6db9e534-c2e8-413e-879f-9a926f0014af" />


## How to compile

IDE: Visual studio 2022 with UWP sdk installed.

Minimum OS version: Windows 10 1903,18362 or higher

## What you need to prepare to get car online service work

### Step 1 

Go to https://developer.tesla.com/docs/fleet-api to create Tesla account

### Step 2

Apply for API key and register/activate your partner account one time. There is code in the project, but there is no button to call it. Do your own magic to activate it.s

### Step 3

Replace the API key within the app. Simply search `//Replace with you own key`. And you will find where to replace the client_id and client_secret. (Sorry I put API key set all over the places. Be sure to replace them all)

e.g. Api key in US/EU is different from Mainland China

## What you need to get map service work

Go to https://azure.microsoft.com/en-us/products/azure-maps/ to apply Bing maps api if you want to get the map control working in release version.

The service is okay without token in debug mode.

Search `MapServiceToken` and replace it with your own token.

## More info

You are free to modify and distribute it to the store. Just follow the GPL3.0 license.

Any activities that violates the privacy policy including store the users' data into your own server. Or monitor users' data will not be allowed.
