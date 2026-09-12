using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeslaMurphy.Services
{
    internal class VehicleCommandsServices
    {
        public static Task<HttpService.CommandResponse> AutoConditioningOnPostAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
            => SendClimateAsync(base_url, access_token, vehicle_tag, true, cts);

        public static Task<HttpService.CommandResponse> AutoConditioningOffPostAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
            => SendClimateAsync(base_url, access_token, vehicle_tag, false, cts);

        public static Task<HttpService.CommandResponse> SetDoorLockAsync(string baseUrl, string token, string vin, bool locked, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/"
                + (locked ? "door_lock" : "door_unlock");
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token);
        }
        public static Task<HttpService.CommandResponse> SetScheduleAsync(string baseUrl, string token, string vin, bool departure, string json, CancellationTokenSource cts)
        {
            var endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/"
                + (departure ? "set_scheduled_departure" : "set_scheduled_charging");
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token, json);
        }
        private static Task<HttpService.CommandResponse> SendClimateAsync(string baseUrl, string token, string vin, bool turnOn, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/"
                + (turnOn ? "auto_conditioning_start" : "auto_conditioning_stop");
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token);
        }
    }
}
