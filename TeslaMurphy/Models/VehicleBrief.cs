using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeslaMurphy.Models
{
    public class VehicleBrief
    {
        public string id { get; set; }
        public string vehicle_id { get; set; }
        public string vin { get; set; }
        public string access_type { get; set; }
        public string display_name { get; set; }
        public string state { get; set; }
        public bool in_service { get; set; }
    }

    public class VehicleListResponse
    {
        public List<VehicleBrief> response { get; set; }
    }
}
