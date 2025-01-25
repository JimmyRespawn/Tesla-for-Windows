using System.Threading;
using System.Threading.Tasks;

namespace TeslaMurphy.Services
{
    internal class VehicleCommandsServices
    {
        public static async Task<string> AutoConditioningOffPostAsync(string base_url, string access_token, string vehicle_tag, CancellationTokenSource cts)
        {
            try
            {
                string endpoint = "/api/1/vehicles/" + vehicle_tag + "/command/auto_conditioning_stop";
                return await TeslaFleetServices.HttpPostRequestAsync(base_url, endpoint, access_token, cts);
            }
            catch
            {
                return null;
            }
        }
    }
}
