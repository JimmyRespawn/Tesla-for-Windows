using Newtonsoft.Json;

namespace TeslaMurphy.Models
{
    public class CarDataResponse
    {
        public CarData response{ get; set; }
    }

    public class CarData
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public long vehicle_id { get; set; }
        public string vin { get; set; }
        public string access_type { get; set; }
        public string state { get; set; }
        public bool in_service { get; set; }
        public bool calendar_enabled { get; set; }
        public int api_version { get; set; }
        public bool ble_autopair_enrolled { get; set; }

        public ChargeStateData charge_state { get; set; }
        public GuiSettings gui_settings { get; set; }
        public ParkedAccessory parked_accessory { get; set; }
        public VihicleState vehicle_state { get; set; }
        public ClimateState climate_state { get; set; }
        public VehicleConfig vehicle_config { get; set; }
    }

    public class VehicleConfig
    {
        public string car_type { get; set; }
        public string driver_assist { get; set; }
        public string exterior_color { get; set; }
        public string wheel_type { get; set; }
    }

    public class ChargeStateData
    {
        public bool battery_heater_on { get; set; }
        public int battery_level { get; set; }
        public double battery_range { get; set; }
        public int charge_amps { get; set; }
        public int charge_current_request { get; set; }
        public int charge_current_request_max { get; set; }
        public bool charge_enable_request { get; set; }
        public double charge_energy_added { get; set; }
        public int charge_limit_soc { get; set; }
        public int charge_limit_soc_max { get; set; }
        public int charge_limit_soc_min { get; set; }
        public int charge_limit_soc_std { get; set; }
        public double charge_miles_added_ideal { get; set; }
        public double charge_miles_added_rated { get; set; }
        public bool charge_port_cold_weather_mode { get; set; }
        public string charge_port_color { get; set; }
        public bool charge_port_door_open { get; set; }
        public string charge_port_latch { get; set; }
        public double charge_rate { get; set; }
        public int charger_actual_current { get; set; }
        public int charger_pilot_current { get; set; }
        public int charger_power { get; set; }
        public int charger_voltage { get; set; }
        //Charging, Disconnected
        public string charging_state { get; set; }
        public string conn_charge_cable { get; set; }
        public double est_battery_range { get; set; }
        public double ideal_battery_range { get; set; }
        public string fast_charger_brand { get; set; }
        public bool fast_charger_present { get; set; }
        public bool preconditioning_enabled { get; set; }
        public bool off_peak_charging_enabled { get; set; }
        //Scheduled charging time
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string scheduled_charging_mode { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool scheduled_charging_pending { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int scheduled_charging_start_time_app { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int scheduled_charging_start_time_minutes { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int scheduled_departure_time { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int scheduled_departure_time_minutes { get; set; }
        public long timestamp { get; set; }
    }

    public class GuiSettings
    {
        public bool gui_24_hour_time { get; set; }
        public string gui_charge_rate_units { get; set; }
        public string gui_distance_units { get; set; }
        public string gui_temperature_units { get; set; }
        public string gui_tirepressure_units { get; set; }
        public long timestamp { get; set; }
    }

    public class ParkedAccessory
    {
        public string car_type { get; set; }
        public string driver_assist { get; set; }
        public string wheel_type { get; set; }
    }

    public class VihicleState
    {
        public string car_version { get; set; }
        public string vehicle_name { get; set; }
        public bool locked { get; set; }
    }

    public class  ClimateState
    {
        public bool is_climate_on { get; set; }
        public bool is_auto_conditioning_on { get; set; }
        public double outside_temp { get; set; }
        public double inside_temp { get; set; }
    }
}
