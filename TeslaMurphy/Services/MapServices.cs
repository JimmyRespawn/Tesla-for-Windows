using System;
using System.Linq;
using Windows.Services.Maps;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TeslaMurphy.Services
{
    internal class MapServices
    {
        public static async Task<List<MapLocation>> SearchAddressAsync(string address)
        {
            // Search address
            var result = await MapLocationFinder.FindLocationsAsync(address, null, 5);

            // 检查结果是否成功
            if (result.Status == MapLocationFinderStatus.Success && result.Locations.Count > 0)
                return result.Locations.ToList();
            return null;
        }
    }
}
