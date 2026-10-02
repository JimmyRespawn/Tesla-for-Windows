using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TeslaMurphy.Models;

namespace TeslaMurphy.Services
{
    internal sealed class VehicleSoftwareService
    {
        private readonly string vin, baseUrl, commandUrl;
        public VehicleSoftwareService(string vehicleVin)
        {
            if (string.IsNullOrWhiteSpace(vehicleVin)) throw new ArgumentException("No vehicle selected.");
            vin = vehicleVin;
            baseUrl = AppSettings.Instance.Base_URL;
            commandUrl = TeslaConfiguration.CommandBaseUrl;
        }
        private string Path => "/api/1/vehicles/" + Uri.EscapeDataString(vin);
        private static JObject Parse(HttpService.CommandResponse result)
        {
            if (!string.IsNullOrEmpty(result.TransportError)) throw new InvalidOperationException(result.TransportError);
            if (result.StatusCode < 200 || result.StatusCode >= 300)
                throw new InvalidOperationException("Request failed (HTTP " + result.StatusCode + "). Check vehicle connectivity and authorization, then refresh.");
            var root = JObject.Parse(result.Body ?? "{}");
            if (root["error"] != null && root["error"].Type != JTokenType.Null && !string.IsNullOrEmpty((string)root["error"]))
                throw new InvalidOperationException("Tesla rejected the request. Refresh the vehicle status.");
            return root["response"] as JObject ?? throw new InvalidOperationException("Vehicle data is unavailable.");
        }
        public async Task<JObject> RefreshAsync(Action<string> reportProgress = null)
        {
            Guard();
            var response = await ReadVehicleDataAsync();
            // The shared HTTP layer already refreshes an expired token once.
            // For a remaining 401, allow one wake + read recovery, never a loop.
            if (response.StatusCode == 401)
            {
                reportProgress?.Invoke("Waking vehicle before checking again…");
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    var awake = Parse(await HttpService.SendManagementAsync(HttpMethod.Post, baseUrl,
                        Path + "/wake_up", timeout.Token, "{}"));
                    var wakeVin = (string)awake["vin"];
                    if (!string.IsNullOrEmpty(wakeVin) && !string.Equals(wakeVin, vin, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Wake response does not match the selected vehicle.");
                    string state = (string)awake["state"];
                    if (state != "online" && state != "asleep" && state != "offline" && state != "waking")
                        throw new InvalidOperationException("Unable to confirm the wake request. Please check again later.");
                    if (state != "online") await Task.Delay(TimeSpan.FromSeconds(10), timeout.Token);
                }
                reportProgress?.Invoke("Checking vehicle again…");
                response = await ReadVehicleDataAsync();
                if (response.StatusCode == 401)
                    throw new InvalidOperationException("Still unauthorized (HTTP 401) after waking. Please sign in again or check vehicle permissions.");
            }
            var data = Parse(response);
            if (!string.Equals((string)data["vin"], vin, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The response does not match the selected vehicle.");
            return data;
        }
        private async Task<HttpService.CommandResponse> ReadVehicleDataAsync()
        {
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                return await HttpService.SendManagementAsync(HttpMethod.Get, baseUrl,
                    Path + "/vehicle_data?endpoints=vehicle_state%3Bcharge_state%3Bvehicle_config", timeout.Token);
        }
        internal static double? EstimateFullRangeKm(JObject charge)
        {
            // Fleet API range values are in miles, independent of display preferences.
            var rangeToken = charge?["battery_range"];
            var levelToken = charge?["battery_level"];
            if (rangeToken == null || levelToken == null
                || (rangeToken.Type != JTokenType.Float && rangeToken.Type != JTokenType.Integer)
                || (levelToken.Type != JTokenType.Float && levelToken.Type != JTokenType.Integer)) return null;
            double range = (double)rangeToken, level = (double)levelToken;
            if (double.IsNaN(range) || double.IsInfinity(range) || range <= 0
                || double.IsNaN(level) || level <= 0 || level > 100) return null;
            return range * 100 / level * 1.609344;
        }
        public async Task SendAsync(bool cancel)
        {
            Guard();
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                // Do not retry an ambiguous installation response: the vehicle may have accepted it.
                var response = Parse(await HttpService.SendCommandAsync(commandUrl,
                    Path + "/command/" + (cancel ? "cancel_software_update" : "schedule_software_update"),
                    AppSettings.Instance.Access_token, timeout.Token, cancel ? "{}" : "{\"offset_sec\":0}"));
                if ((bool?)response["result"] != true)
                    throw new InvalidOperationException("The vehicle did not accept the command. Refresh its status and check the Tesla app.");
            }
        }
        private static void Guard()
        {
            if (AppSettings.Instance.IsTestMode) throw new InvalidOperationException("Software updates are unavailable in demo mode.");
        }
    }
}
