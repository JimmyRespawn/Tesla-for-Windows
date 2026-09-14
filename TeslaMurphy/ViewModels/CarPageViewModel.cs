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

        private int vehicleLoadVersion;
        private static string VehicleCacheKey(string vin)
            => "vehicledata-" + new string(vin.Where(char.IsLetterOrDigit).ToArray());

        public async Task Intitalize(string vehicle_tag, bool isForceRefresh = false)
        {
            int version = ++vehicleLoadVersion;
            IsLoading = true;
            try
            {
                if (CarList == null)
                    await GetCarListAsync();
                if (version != vehicleLoadVersion) return;
                string vin = vehicle_tag;
                if (CarList != null && !CarList.Any(x => x.vin == vin))
                    vin = AppSettings.Instance.Current_carvin;
                if (string.IsNullOrWhiteSpace(vin)) vin = AppSettings.Instance.Current_carvin;
                if (string.IsNullOrWhiteSpace(vin)) { CarData = null; return; }

                AppSettings.Instance.Current_carvin = vin;
                Windows.Storage.ApplicationData.Current.LocalSettings.Values["maincarvin"] = vin;
                if (CarData?.vin != vin)
                {
                    CarData = null;
                    MapIcons = new ObservableCollection<MapIcon>();
                    UserMapLocations = new ObservableCollection<MapLocation>();
                }

                var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                string timeKey = VehicleCacheKey(vin) + "-time";
                long saved;
                bool expired = isForceRefresh || !values.ContainsKey(timeKey)
                    || !long.TryParse(values[timeKey].ToString(), out saved)
                    || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - saved > 4 * 3600;
                if (!AppSettings.Instance.IsTestMode && expired)
                {
                    bool awake = await CheckIfCarisAwakeAsync(vin);
                    if (version != vehicleLoadVersion) return;
                    if (!awake)
                    {
                        await WakeCarAsync(vin);
                        await Task.Delay(18000);
                        if (version != vehicleLoadVersion) return;
                    }
                }
                await GetCarInfoAsync(vin, expired);
            }
            finally
            {
                if (version == vehicleLoadVersion) IsLoading = false;
            }
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
            if (string.IsNullOrWhiteSpace(vehicle_tag)) return;
            int version = vehicleLoadVersion;
            try
            {
                string content = null;
                string cache = VehicleCacheKey(vehicle_tag);
                if (AppSettings.Instance.IsTestMode)
                    content = await UWPGeneralHelper.GetTestFileAsync("vehicledata.json");
                else
                {
                    if (!isGettingOnline)
                    {
                        try { content = await UWPGeneralHelper.GetCacheFileStringAsync(cache + ".json"); }
                        catch { }
                    }
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        content = await VehicleEndpointsServices.VehicleDataGetAsync(
                            AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token,
                            vehicle_tag, new CancellationTokenSource());
                        var live = JsonConvert.DeserializeObject<CarDataResponse>(content);
                        if (live?.response?.vin != vehicle_tag) return;
                        await UWPGeneralHelper.SaveStringToCacheFileAsync(cache + ".json", content);
                        Windows.Storage.ApplicationData.Current.LocalSettings.Values[cache + "-time"]
                            = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    }
                }
                var data = JsonConvert.DeserializeObject<CarDataResponse>(content)?.response;
                if (data != null && (AppSettings.Instance.IsTestMode || data.vin == vehicle_tag)
                    && version == vehicleLoadVersion && AppSettings.Instance.Current_carvin == vehicle_tag)
                    CarData = data;
            }
            catch (Exception)
            {
                // A failed response must never replace this or another vehicle's cached data.
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
                    if (responseString == "Unauthorized") return false;
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
                    // Give each attempt its own timeout, including after waking the vehicle.
                    cts.CancelAfter(TimeSpan.FromSeconds(45));
                    var response = await send(cts);
                    cts.CancelAfter(Timeout.InfiniteTimeSpan);
                    if (response.StatusCode == 500 && attempt == 0)
                    {
                        using (var wakeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                        {
                            // Wake-up uses the regional Fleet API, not the signing proxy.
                            string wakeResponse = await VehicleEndpointsServices.WakeUpPostAsync(
                                AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, wakeCts);
                            bool wakeAccepted = false;
                            bool online = false;
                            try
                            {
                                var wake = Newtonsoft.Json.Linq.JObject.Parse(wakeResponse ?? "");
                                var state = (string)wake["response"]?["state"];
                                wakeAccepted = !string.IsNullOrEmpty(state) && wake["error"] == null;
                                online = state == "online";
                            }
                            catch (Newtonsoft.Json.JsonException) { }
                            if (!wakeAccepted)
                            {
                                VehicleCommandError = "HTTP 500. Unable to wake the vehicle; the command was not retried. Please try again later.";
                                return false;
                            }
                            // Waking is asynchronous. Allow a short startup interval if needed.
                            if (!online) await Task.Delay(TimeSpan.FromSeconds(10), wakeCts.Token);
                        }
                        continue;
                    }
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

        public Task<bool> RefreshToken()
            => TeslaFleetServices.EnsureSessionAsync();

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
                        var position = ChinaCoordinates.Convert(lat.Value, lon.Value, TeslaConfiguration.VehicleCoordinateConversion);
                        return new Geopoint(new BasicGeoposition { Latitude = position[0], Longitude = position[1] });
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
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        responseString = await VehicleEndpointsServices.NearbyChargingSitesGetAsync(AppSettings.Instance.Base_URL, AppSettings.Instance.Access_token, vehicle_tag, cts);
                    }
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
                    if (sitesResponse?.response == null
                        || AppSettings.Instance.Current_carvin != vehicle_tag) return false;
                    foreach (var destination in sitesResponse.response.destination_charging ?? new List<ChargingLocation>())
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
                    foreach (var supercharger in sitesResponse.response.superchargers ?? new List<SuperchargerLocation>())
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

        private readonly SemaphoreSlim vehicleListLock = new SemaphoreSlim(1, 1);
        public async Task<List<VehicleBrief>> GetCarListAsync(bool isForceGetOnlineCache = false)
        {
            await vehicleListLock.WaitAsync();
            try
            {
                if (!isForceGetOnlineCache && CarList != null) return CarList.ToList();
                string json = AppSettings.Instance.IsTestMode
                    ? await UWPGeneralHelper.GetTestFileAsync("vehiclelist.json")
                    : await VehicleEndpointsServices.VehiclesListGetAsync(AppSettings.Instance.Base_URL,
                        AppSettings.Instance.Access_token, "", new CancellationTokenSource());
                var response = JsonConvert.DeserializeObject<VehicleListResponse>(json)?.response;
                if (response == null) return CarList?.ToList();
                var cars = response.Where(x => !string.IsNullOrWhiteSpace(x.vin))
                    .GroupBy(x => x.vin).Select(x => x.First()).ToList();
                CarList = new ObservableCollection<VehicleBrief>(cars);
                var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                string selected = AppSettings.Instance.Current_carvin;
                if (string.IsNullOrWhiteSpace(selected) && values.ContainsKey("maincarvin"))
                    selected = values["maincarvin"] as string;
                if (!cars.Any(x => x.vin == selected)) selected = cars.FirstOrDefault()?.vin;
                AppSettings.Instance.Current_carvin = selected;
                if (selected == null)
                {
                    values.Remove("maincarvin");
                    CarData = null;
                    MapIcons = new ObservableCollection<MapIcon>();
                    MainPage.Instance.ShowToast("No car is linked to the account");
                }
                else values["maincarvin"] = selected;
                return cars;
            }
            catch { return CarList?.ToList(); }
            finally { vehicleListLock.Release(); }
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
