using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace TeslaMurphy.Models
{
    public class ChargerLocation
    {
    }

    public class NearbyChargingSitesResponse
    {
        public NearbyChargingSites response { get; set; }
    }

    public class NearbyChargingSites
    {
        public List<ChargingLocation> destination_charging { get; set; }
        public List<SuperchargerLocation> superchargers { get; set; }
        public long congestion_sync_time_utc_secs { get; set; }
        public long timestamp { get; set; }
    }


    public class ChargingLocation
    {
        public Location location { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public double distance_miles { get; set; }
        public string amenities { get; set; }
    }
    public class SuperchargerLocation
    {
        public Location location { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public double distance_miles { get; set; }
        public int available_stalls { get; set; }
        public int total_stalls { get; set; }
        public bool site_closed { get; set; }
        public string amenities { get; set; }
        public string billing_info { get; set; }
    }
    public class Location
    {
        public double lat { get; set; }
        public double @long { get; set; }
    }
}
