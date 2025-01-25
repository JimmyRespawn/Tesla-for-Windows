

using System.Threading;
using System.Threading.Tasks;
using TeslaMurphy.Models;

namespace TeslaMurphy.Services
{
    internal class VehicleEndpointsServices
    {
        /// <summary>
        /// list
        /// returns a list of vehicles associated with the account. The response includes the vehicle id, vin, and display name. Nomally 100 vehicles per page.
        /// </summary>
        /// <param name="base_url"></param>
        /// <param name="access_token"></param>
        /// <param name="vehicle_tag"></param>
        /// <param name="cts"></param>
        /// <returns></returns>
        public static async Task<string> VehiclesListGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> MobileEnabledGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/mobile_enabled";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> ServiceGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/service_data";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> NearbyChargingSitesGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/nearby_charging_sites";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }


        public static async Task<string> ReleaseNotesGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/release_notes";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> DriversGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/drivers";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// vehicle_data
        /// Makes a live call to the vehicle to fetch realtime information. Regularly polling this endpoint is not recommended and will exceed rate limits (see Subscription Plans) quickly. Instead, Fleet Telemetry allows the vehicle to push data directly to a server whenever it is online.
        /// For vehicles running firmware versions 2023.38+, location_data is required to fetch vehicle location.This will result in a location sharing icon to show on the vehicle UI.
        /// Update time: 2024/9/13
        /// </summary>
        /// <param name="base_url">NA/EU/CN are different</param>
        /// <param name="access_token">expired every 8 hours</param>
        /// <param name="vehicle_tag">id or vin</param>
        /// <param name="cts">for cancelling the call if needed</param>
        /// <returns>json data</returns>
        public static async Task<string> VehicleDataGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/vehicle_data";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> WakeUpPostAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/wake_up";
                return await TeslaFleetServices.HttpPostRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> RecentAlertsGetAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/recent_alerts";
                return await TeslaFleetServices.HttpGetRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }
    }
}
