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
        public static Task<HttpService.CommandResponse> SetChargeLimitAsync(string baseUrl, string token, string vin, int percent, CancellationTokenSource cts)
        {
            if (percent < 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/set_charge_limit";
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token,
                Newtonsoft.Json.JsonConvert.SerializeObject(new { percent }));
        }

        public static Task<HttpService.CommandResponse> SetChargePortAsync(string baseUrl, string token, string vin, bool open, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/"
                + (open ? "charge_port_door_open" : "charge_port_door_close");
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token);
        }

        public static Task<HttpService.CommandResponse> RemoteStartDriveAsync(string baseUrl, string token, string vin, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/remote_start_drive";
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token);
        }

        public static Task<HttpService.CommandResponse> FlashLightsAsync(string baseUrl, string token, string vin, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/flash_lights";
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token);
        }

        public static Task<HttpService.CommandResponse> OpenFrunkAsync(string baseUrl, string token, string vin, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/actuate_trunk";
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token,
                Newtonsoft.Json.JsonConvert.SerializeObject(new { which_trunk = "front" }));
        }

        public static Task<HttpService.CommandResponse> OpenRearTrunkAsync(string baseUrl, string token, string vin, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/actuate_trunk";
            // The official signed proxy maps rear to OpenTrunk, not CloseTrunk.
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token,
                Newtonsoft.Json.JsonConvert.SerializeObject(new { which_trunk = "rear" }));
        }

        public static Task<HttpService.CommandResponse> SetWindowsAsync(string baseUrl, string token, string vin, bool vent, CancellationTokenSource cts)
        {
            string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(vin) + "/command/window_control";
            // Model 3 and the signed vehicle-command protocol do not require
            // caller coordinates for window_control. Never fabricate location.
            return HttpService.SendCommandAsync(baseUrl, endpoint, token, cts.Token,
                Newtonsoft.Json.JsonConvert.SerializeObject(new { command = vent ? "vent" : "close" }));
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
