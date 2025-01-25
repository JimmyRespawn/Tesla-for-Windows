using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeslaMurphy.Models
{
    public class DriversResponse
    {
        public List<Driver> response { get; set; }
    }

    public class Driver
    {
        public long my_tesla_unique_id { get; set; }
        public long user_id { get; set; }
        public string user_id_s { get; set; }
        public string driver_first_name { get; set; }
        public string driver_last_name { get; set; }
    }

    public class MeResponse
    {
        public Me response { get; set; }
    }

    public class Me
    {
        public string email { get; set; }
        public string full_name { get; set; }
        public string profile_image_url { get; set; }
        public string vault_uuid { get; set; }
    }
}
