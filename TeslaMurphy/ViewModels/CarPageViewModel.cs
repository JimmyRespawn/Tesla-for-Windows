using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using TeslaMurphy.Controls;
using TeslaMurphy.Helpers;
using TeslaMurphy.Models;
using TeslaMurphy.Services;
using TeslaMurphy.Views;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;

namespace TeslaMurphy.ViewModels
{
    public partial class CarPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private CarData carData;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<MapIcon> mapIcons;

        [ObservableProperty]
        private MapColorScheme mapColorScheme;

        [ObservableProperty]
        private ObservableCollection<MapLocation> userMapLocations;

        [ObservableProperty]
        private ObservableCollection<VehicleBrief> carList;

        public CarPageViewModel()
        {
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string tokenTime = "";
            if (localSettings.Values.ContainsKey("accesstokentime"))
                tokenTime = localSettings.Values["accesstokentime"].ToString();
            if (string.IsNullOrEmpty(tokenTime) || tokenTime != DateTime.Now.Date.ToString())
                if (localSettings.Values.ContainsKey("refreshtoken"))
                    RefreshToken();
        }

        public async Task Intitalize(string vehicle_tag, bool isForceRefresh = false)
        {
            IsLoading = true;
            //Get car vin in the accountr, if there is no vin
            if (string.IsNullOrEmpty(AppSettings.Instance.Current_carvin))
                await GetCarListAsync();

            bool isCacheExpired = false;
            if (isForceRefresh)
                isCacheExpired = true;

            if (!isCacheExpired)
            {
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey("cardatacachetime"))
                {
                    // retrieve the current time
                    DateTime currentTime = DateTime.Now;
                    DateTime lastCacheTime = DateTime.Parse(localSettings.Values["cardatacachetime"].ToString());
                    // check if the cache is expired
                    if ((currentTime - lastCacheTime).TotalHours > 4)
                    {
                        // Expired every 4 hours
                        // To be editted
                        isCacheExpired = true;
                    }
                }
                else
                    isCacheExpired = true;
            }

            if (!AppSettings.Instance.IsTestMode)
            {
                if(AppSettings.Instance.Current_carvin != null && isCacheExpired)
                {
                    bool isCarAwake = await CheckIfCarisAwakeAsync(AppSettings.Instance.Current_carvin);
                    if (!isCarAwake)
                    {
                        await WakeCarAsync(AppSettings.Instance.Current_carvin);
                        await Task.Delay(18000);// Get info after 10s , 15秒不够，目前在测试18秒
                    }
                }
            }

            if (AppSettings.Instance.Current_carvin != null)
                await GetCarInfoAsync(AppSettings.Instance.Current_carvin, isCacheExpired);
            IsLoading = false;
        }

        public async Task<string> ShowLoginDialog()
        {
            LoginContentDialog dialog = new LoginContentDialog();
            dialog.DefaultButton = ContentDialogButton.Primary;
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                //Login or experience the app
                bool isTestMode = dialog.IsTestMode;
                if(isTestMode)
                {
                    AppSettings.Instance.IsTestMode = true;
                }
                else
                {
                    if (!await TeslaConfiguration.SelectLoginRegionAsync(dialog.Region)) return null;
                    string authRequestUrl = await TeslaFleetServices.GenerateAuthorizeUriAsync();
                    return authRequestUrl;
                }
            }
            else
            {
                //Experience mode
                AppSettings.Instance.IsTestMode = true;
            }
            return null;
        }

        public async Task GetCarInfoAsync(string vehicle_tag, bool isGettingOnline = true)
        {
            try
            {
                string responseString = "";
                if (!AppSettings.Instance.IsTestMode)
                {
                    if (isGettingOnline)
                    {
                        responseString = await VehicleEndpointsServices.VehicleDataGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                        // mark save time
                        if (!responseString.Contains("Unauthorized"))
                        {
                            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                            localSettings.Values["cardatacachetime"] = DateTime.Now.ToString();
                            await UWPGeneralHelper.SaveStringToCacheFileAsync("vehicledata.json", responseString);
                        }
                    }
                    else
                    {
                        responseString = await UWPGeneralHelper.GetCacheFileStringAsync("vehicledata.json");
                        if (string.IsNullOrEmpty(responseString))
                        {
                            responseString = await VehicleEndpointsServices.VehicleDataGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                            // mark save time
                            if (!responseString.Contains("Unauthorized"))
                            {
                                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                                localSettings.Values["cardatacachetime"] = DateTime.Now.ToString();
                                await UWPGeneralHelper.SaveStringToCacheFileAsync("vehicledata.json", responseString);
                            }
                        }
                    }
                }
                else
                {
                    //Load local file to test UI
                    responseString = await UWPGeneralHelper.GetTestFileAsync("vehicledata.json");
                }
                if (!string.IsNullOrEmpty(responseString))
                {
                    CarDataResponse carDataResponse = JsonConvert.DeserializeObject<CarDataResponse>(responseString);
                    CarData = carDataResponse.response;
                }
            }
            catch//To be editted
            {
                //Log the responseString
            }
        }

        //Getting info before awake
        public async Task<bool> WakeCarAsync(string vehicle_tag)
        {
            if (!AppSettings.Instance.IsTestMode)
            {
                //string endpoint = "/api/1/vehicles/" + vehicle_tag + "/wake_up";
                //string responseString = await TeslaFleetServices.HttpPostRequestAsync(AppSettings.Instance.Base_URL, endpoint, AppSettings.Instance.Access_token, new CancellationTokenSource());
                string responseString = await VehicleEndpointsServices.WakeUpPostAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                if (!string.IsNullOrEmpty(responseString))
                {
                    if (responseString == "Unauthorized")
                    {
                        await RefreshToken();
                        //responseString = await VehicleEndpointsServices.WakeUpPostAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                        //return true;
                    }
                    else if (responseString == "TooManyRequests")
                    {
                        // Notify users that call to much
                        MainPage.Instance.ShowToast("Limitaion exceeded, use mobile to wake your car", "\uEA6A");
                    }
                    return true;
                }
            }
            else
            {
                // do nothing
            }
            return false;
        }

        public async Task<string> GetReleaseNotesAsync(string vehicle_tag)
        {
            try
            {
                IsLoading = true;
                string responseString = "";
                if (!AppSettings.Instance.IsTestMode)
                {
                    responseString = await VehicleEndpointsServices.ReleaseNotesGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                    if (!string.IsNullOrEmpty(responseString))
                    {
                        if (responseString == "Unauthorized")
                        {
                            await RefreshToken();
                        }
                    }
                }
                else
                {
                    responseString = await UWPGeneralHelper.GetTestFileAsync("releasenotes.json");
                }
                IsLoading = false;
                return responseString;
            }
            catch
            {
                IsLoading = false;
                return null;
            };
        }

        public async Task<string> GetDriversAsync(string vehicle_tag)
        {
            try
            {
                IsLoading = true;
                string responseString = "";
                if (!AppSettings.Instance.IsTestMode)
                {
                    responseString = await VehicleEndpointsServices.DriversGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                    if (!string.IsNullOrEmpty(responseString))
                    {
                        if (responseString == "Unauthorized")
                        {
                            await RefreshToken();
                        }
                    }
                }
                else
                {
                    responseString = await UWPGeneralHelper.GetTestFileAsync("drivers.json");
                }
                IsLoading = false;
                return responseString;
            }
            catch
            {
                IsLoading = false;
                return null;
            };
        }

        public async Task<string> GetSerivceAsync(string vehicle_tag)
        {
            try
            {
                IsLoading = true;
                string responseString = "";
                if (!AppSettings.Instance.IsTestMode)
                {
                    responseString = await VehicleEndpointsServices.ServiceGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                    if (!string.IsNullOrEmpty(responseString))
                        if (responseString == "Unauthorized")
                            await RefreshToken();
                }
                else
                {
                    responseString = await UWPGeneralHelper.GetTestFileAsync("service.json");
                }
                IsLoading = false;
                return responseString;
            }
            catch
            {
                IsLoading = false;
                return null;
            };
        }

        public Task<bool> TurnOffACAsync(string vehicle_tag) => SetClimateAsync(vehicle_tag, false);

        public Task<bool> TurnOnACAsync(string vehicle_tag) => SetClimateAsync(vehicle_tag, true);

        public string VehicleCommandError { get; private set; }

        public Task<bool> SetDoorLockAsync(string vin, bool locked)
            => ExecuteVehicleCommandAsync(vin, cts => VehicleCommandsServices.SetDoorLockAsync(
                TeslaConfiguration.CommandBaseUrl, AppSettings.Instance.Access_token, vin, locked, cts));

        private Task<bool> SetClimateAsync(string vehicle_tag, bool turnOn)
            => ExecuteVehicleCommandAsync(vehicle_tag, cts => turnOn
                ? VehicleCommandsServices.AutoConditioningOnPostAsync(TeslaConfiguration.CommandBaseUrl, AppSettings.Instance.Access_token, vehicle_tag, cts)
                : VehicleCommandsServices.AutoConditioningOffPostAsync(TeslaConfiguration.CommandBaseUrl, AppSettings.Instance.Access_token, vehicle_tag, cts));

        private async Task<bool> ExecuteVehicleCommandAsync(string vehicle_tag, Func<CancellationTokenSource, Task<HttpService.CommandResponse>> send)
        {
            VehicleCommandError = "The vehicle did not confirm the command.";
            if (AppSettings.Instance.IsTestMode || string.IsNullOrWhiteSpace(vehicle_tag)) return false;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var response = await send(cts);
                    if (response.StatusCode == 401 && attempt == 0 && await RefreshToken()) continue;
                    if (response.TransportError != null)
                    {
                        VehicleCommandError = response.TransportError;
                        return false;
                    }
                    string detail = "";
                    try
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(response.Body ?? "");
                        var result = json["response"] as Newtonsoft.Json.Linq.JObject;
                        var accepted = result?["result"];
                        if (response.StatusCode >= 200 && response.StatusCode < 300 &&
                            accepted?.Type == Newtonsoft.Json.Linq.JTokenType.Boolean && (bool)accepted) return true;
                        foreach (var field in new[] { result?["reason"], json["error"], json["error_description"] })
                            if (field?.Type == Newtonsoft.Json.Linq.JTokenType.String && !string.IsNullOrWhiteSpace((string)field))
                                detail += (detail.Length == 0 ? "" : "\n") + (string)field;
                    }
                    catch (Newtonsoft.Json.JsonException) { detail = "The server returned an unexpected command response."; }
                    // Never include tokens, request headers or the complete response body in the UI.
                    foreach (string secret in new[] { AppSettings.Instance.Access_token, AppSettings.Instance.Refresh_token, AppSettings.Instance.Client_secret })
                        if (!string.IsNullOrEmpty(secret)) detail = detail.Replace(secret, "[redacted]");
                    if (detail.Length > 1000) detail = detail.Substring(0, 1000);
                    VehicleCommandError = "HTTP " + response.StatusCode + "\n" + detail;
                    string lower = detail.ToLowerInvariant();
                    if (lower.Contains("vehicle command protocol") || lower.Contains("signed") || lower.Contains("virtual key"))
                        VehicleCommandError += "\nThe vehicle requires signed commands. Check the command proxy and virtual key pairing.";
                    else if (response.StatusCode == 401)
                        VehicleCommandError += "\nAuthorization expired. Please sign in again.";
                    else if (response.StatusCode == 403)
                        VehicleCommandError += "\nAccess denied. Check vehicle permissions and command signing.";
                    else if (response.StatusCode == 408 || lower.Contains("asleep") || lower.Contains("offline"))
                        VehicleCommandError += "\nWake the vehicle and try again once it is online.";
                    return false;
                }

            }
            return false;
        }

        public async Task<bool> ShowReleaseNotes(string releaseNotes)
        {
            ReleaseNotesContentDialog dialog = new ReleaseNotesContentDialog();
            dialog.ReleaseNotesString = releaseNotes;
            dialog.DefaultButton = ContentDialogButton.Primary;
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                UWPGeneralHelper.OpenInDefaultBrowser("https://www." + AppSettings.Instance.Region_URL + "/support/software-updates#release-note-videos");
            return true;
        }

        public async Task<bool> ShowDrivers(string drivers)
        {
            DriversContentDialog dialog = new DriversContentDialog();
            dialog.DriversJsonString = drivers;
            ContentDialogResult result = await dialog.ShowAsync();
            return true;
        }

        public async Task<bool> ShowSchedule(ChargeStateData chargeStateData)
        {
            ScheduleContentDialog dialog = new ScheduleContentDialog();
            string vin = CarData?.vin;
            if (!string.IsNullOrWhiteSpace(vin))
                dialog.SaveScheduleAsync = (departure, json) => ExecuteVehicleCommandAsync(vin,
                    cts => VehicleCommandsServices.SetScheduleAsync(TeslaConfiguration.CommandBaseUrl,
                        AppSettings.Instance.Access_token, vin, departure, json, cts));
            dialog.GetCommandError = () => VehicleCommandError;
            dialog.chargeStateData = chargeStateData;
            ContentDialogResult result = await dialog.ShowAsync();
            return true;
        }

        public async Task<bool> ShowService(string service)
        {
            if (string.IsNullOrEmpty(service))
            {
                await DisplayPopout.dualButton("Service", "Not in service.", "OK", "CANCEL");
            }
            else
            {
                var result = Newtonsoft.Json.JsonConvert.DeserializeObject<ServiceResponse>(service);
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Service status: " + "In service");
                if(result.response.service_visit_number != null)
                    stringBuilder.AppendLine("Visit number: " + result.response.service_visit_number);
                if (result.response.service_etc != null)
                    stringBuilder.AppendLine("Estimated time: " + result.response.service_etc);
                await DisplayPopout.dualButton("Service", stringBuilder.ToString(), "OK", "CANCEL");
            }
            return true;
        }

        public async Task<bool> ShowClimate(ClimateState climateState)
        {
            if(climateState != null)
            {
                ClimateContentDialog dialog = new ClimateContentDialog();
                dialog.climateStateData = climateState;
                string vin = CarData?.vin;
                if (!string.IsNullOrWhiteSpace(vin))
                    dialog.ChangeClimateAsync = enabled => enabled ? TurnOnACAsync(vin) : TurnOffACAsync(vin);
                dialog.GetCommandError = () => VehicleCommandError;
                ContentDialogResult result = await dialog.ShowAsync();
            }
            return true;
        }

        public async Task<bool> RefreshToken()
        {
            try
            {
                //access_token expired in 8 hours
                string content = await TeslaFleetServices.RefreshTokenRequestAsync(new CancellationTokenSource());
                var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(content);
                string access_token = result.access_token;
                string refresh_token = result.refresh_token;
                if (refresh_token != null)
                {
                    AppSettings.Instance.Refresh_token = refresh_token;
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["refreshtoken"] = refresh_token;
                }

                if (access_token != null)
                {
                    AppSettings.Instance.Access_token = access_token;
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["accesstoken"] = access_token;
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["accesstokentime"] = DateTime.Now.Date.ToString();
                    return true;
                }
            }
            catch
            {

            }
            return false;
        }

        public string VehicleLocationError { get; private set; }

        public async Task<Geopoint> GetVehicleLocationAsync(string vin)
        {
            VehicleLocationError = null;
            if (AppSettings.Instance.IsTestMode) return null;
            if (string.IsNullOrWhiteSpace(vin)) { VehicleLocationError = "No vehicle selected."; return null; }
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        var content = await VehicleEndpointsServices.VehicleLocationGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vin, cts);
                        if (content == "Unauthorized" && attempt == 0 && await RefreshToken()) continue;
                        if (content == "Unauthorized" || content == "Forbidden")
                        { VehicleLocationError = "Vehicle location access denied. Sign in again and allow vehicle location access."; return null; }
                        if (string.IsNullOrWhiteSpace(content))
                        { VehicleLocationError = "Vehicle location request failed or timed out."; return null; }
                        if (content == "RequestTimeout")
                        { VehicleLocationError = "Vehicle is unavailable. Wake it and try again."; return null; }
                        var json = Newtonsoft.Json.Linq.JObject.Parse(content);
                        var drive = json["response"]?["drive_state"];
                        double? lat = (double?)drive?["latitude"];
                        double? lon = (double?)drive?["longitude"];
                        if (!lat.HasValue || !lon.HasValue || double.IsNaN(lat.Value) || double.IsNaN(lon.Value)
                            || Math.Abs(lat.Value) > 90 || Math.Abs(lon.Value) > 180)
                        { VehicleLocationError = "The vehicle did not return a location. Check location permission and vehicle connectivity."; return null; }
                        return new Geopoint(new BasicGeoposition { Latitude = lat.Value, Longitude = lon.Value });
                    }
                }
            }
            catch { VehicleLocationError = "Unable to read vehicle location. Check vehicle connectivity and location permission."; }
            return null;
        }
        public async Task<bool> GetNearbyChargingSitesAsync(string vehicle_tag)
        {
            try
            {
                if (MapIcons == null)
                    MapIcons = new ObservableCollection<MapIcon>();
                else
                    MapIcons.Clear();

                string responseString = "";
                if (!AppSettings.Instance.IsTestMode)
                {
                    var cts = new CancellationTokenSource();
                    responseString = await VehicleEndpointsServices.NearbyChargingSitesGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, cts);
                }
                else
                {
                    //Load local file to test UI
                    responseString = await UWPGeneralHelper.GetTestFileAsync("nearbychargingsites.json");
                }
                //Translation
                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                if (!string.IsNullOrEmpty(responseString))
                {
                    var sitesResponse = JsonConvert.DeserializeObject<NearbyChargingSitesResponse>(responseString);
                    foreach (var destination in sitesResponse.response.destination_charging)
                    {
                        try
                        {
                            var geopoint = new Geopoint(new BasicGeoposition
                            {
                                Latitude = destination.location.lat,
                                Longitude = destination.location.@long
                            });

                            var mapIcon = new MapIcon
                            {
                                Location = geopoint,
                                Title = resourceLoader.GetString("Destination/Text") + ": " + destination.name,
                                ZIndex = 0
                            };

                            MapIcons.Add(mapIcon);
                        }
                        catch { continue; }
                    }
                    foreach (var supercharger in sitesResponse.response.superchargers)
                    {
                        try
                        {
                            var geopoint = new Geopoint(new BasicGeoposition
                            {
                                Latitude = supercharger.location.lat,
                                Longitude = supercharger.location.@long
                            });

                            var mapIcon = new MapIcon
                            {
                                Location = geopoint,
                                Title = resourceLoader.GetString("SuperCharger/Text") + ": " + supercharger.name,
                                ZIndex = 0
                            };

                            MapIcons.Add(mapIcon);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }
        
        public async Task<bool> CheckIfCarisAwakeAsync(string vehicle_tag)
        {
            try
            {
                string responseString = await VehicleEndpointsServices.VehiclesListGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
                if (!string.IsNullOrEmpty(responseString))
                {
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<VehicleListResponse>(responseString);
                    string carIsActive = result.response.Where(X => X.vin == vehicle_tag).FirstOrDefault().state;
                    if(carIsActive == "online")
                        return true;
                    return false;
                }
            }
            catch
            {
            }
            return false;
        }

        public async Task<bool> SearchUserLocationAsync(string address)
        {
            IsLoading = true;
            try
            {
                if (UserMapLocations == null)
                    UserMapLocations = new ObservableCollection<MapLocation>();
                else
                    UserMapLocations.Clear();

                var locations = await MapServices.SearchAddressAsync(address);
                if(locations != null)
                {
                    foreach (var location in locations)
                        UserMapLocations.Add(location);
                }
                IsLoading = false;
                return true;
            }
            catch
            {

            }
            IsLoading = false;
            return false;
        }

        public async Task<List<VehicleBrief>> GetCarListAsync(bool isForceGetOnlineCache = false)
        {
            try
            {
                string carJsonString = "";
                if (!AppSettings.Instance.IsTestMode)
                {
                    Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                    try
                    {
                        // Parse local file string
                        carJsonString = await UWPGeneralHelper.GetCacheFileStringAsync("vehiclelist.json");
                        if (carJsonString.Contains("Unauthorized"))
                        {
                            carJsonString = await VehicleEndpointsServices.VehiclesListGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, "", new CancellationTokenSource());
                            //save string to local cache
                            await UWPGeneralHelper.SaveStringToCacheFileAsync("vehiclelist.json", carJsonString);
                        }
                    }
                    catch
                    {
                        carJsonString = await VehicleEndpointsServices.VehiclesListGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, "", new CancellationTokenSource());
                        //save string to local cache
                        await UWPGeneralHelper.SaveStringToCacheFileAsync("vehiclelist.json", carJsonString);
                    }
                }
                else
                {
                    carJsonString = await UWPGeneralHelper.GetTestFileAsync("vehiclelist.json");
                }

                if (!string.IsNullOrEmpty(carJsonString))
                {
                    //Save json to cache
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<VehicleListResponse>(carJsonString);
                    if(result.response.Count > 1)
                    {
                        //Multiple Cars
                        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                        if (!localSettings.Values.ContainsKey("maincarvin"))
                            Windows.Storage.ApplicationData.Current.LocalSettings.Values["maincarvin"] = result.response[0].vin;
                        AppSettings.Instance.Current_carvin = result.response[0].vin;
                    }
                    else if (result.response.Count == 1)
                    {
                        Windows.Storage.ApplicationData.Current.LocalSettings.Values["maincarvin"] = result.response[0].vin;
                        AppSettings.Instance.Current_carvin = result.response[0].vin;
                    }
                    else
                    {
                        MainPage.Instance.ShowToast("No car is linked to the account");
                        //No Car in the account
                        return null;
                    }
                    return result.response;

                    // Generate car in th menuflyout
                    //foreach (var car in result.response)
                    //{
                    //    CarList.Add(car);
                    //}
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetRecentAlertsAsync(string vehicle_tag)
        {
            try
            {
                string responseString = await VehicleEndpointsServices.RecentAlertsGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, new CancellationTokenSource());
            }
            catch
            {

            }
            return null;
        }
    }
}
