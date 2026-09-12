using Newtonsoft.Json;

namespace TeslaMurphy.Models
{
    public class CarEndpointModel
    {
    }

    public class ServiceResponse
    {
        public Service response { get; set; }
    }

    public class Service
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int status_id { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string service_status { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string service_etc { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string service_visit_number { get; set; }
    }
}
