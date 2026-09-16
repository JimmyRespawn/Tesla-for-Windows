using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace TeslaMurphy.Services
{
    // EPA rows are reference values, never measurements of the vehicle's original pack.
    internal static class RangePresetService
    {
        internal sealed class Match
        {
            public double? Kilometers { get; set; }
            public double? MinimumKm { get; set; }
            public double? MaximumKm { get; set; }
            public string Basis { get; set; } = "EPA";
            public string Description { get; set; }
            public string SourceUrl { get; set; }
        }
        private static string Normalize(string value) => Regex.Replace((value ?? "").ToLowerInvariant(), "[^a-z0-9]", "");
        internal static int? Year(string vin)
        {
            if (vin == null || vin.Length != 17) return null;
            int index = "CDEFGHJKLMNPRST".IndexOf(char.ToUpperInvariant(vin[9]));
            return index < 0 ? (int?)null : 2012 + index;
        }
        internal static Match Find(JObject vehicle, JArray rows, JArray chinaRows = null)
        {
            string vin = ((string)vehicle?["vin"] ?? "").ToUpperInvariant();
            var config = vehicle?["vehicle_config"] as JObject;
            var year = Year(vin);
            string model = Normalize((string)config?["car_type"]);
            if (model == "models2") model = "models";
            string trim = Normalize((string)config?["trim_badging"]);
            string wheel = (string)config?["wheel_type"] ?? "";
            string identity = (year.HasValue ? year.Value + " · " : "") + model + " · " + trim;
            if (!year.HasValue || config == null || string.IsNullOrEmpty(model) || string.IsNullOrEmpty(trim))
                return Missing("Not enough vehicle configuration data to match a reference.");
            if (vin.StartsWith("LRW") && string.Equals((string)config["charge_port_type"], "GB", StringComparison.OrdinalIgnoreCase))
                return FindChina(vin, config, year.Value, model, trim, wheel, chinaRows);
            // A North American factory alone does not imply a US-spec vehicle.
            if (!(vin.StartsWith("5YJ") || vin.StartsWith("7SA") || vin.StartsWith("7G2"))
                || !string.Equals((string)config["charge_port_type"], "US", StringComparison.OrdinalIgnoreCase)
                || (bool?)config["eu_vehicle"] != false || (bool?)config["rhd"] != false)
                return Missing(identity + ": no verified regional reference. US EPA presets are not applied to this vehicle.");
            string vinModel = vin[3] == '3' ? "model3" : vin[3] == 'S' ? "models" : vin[3] == 'X' ? "modelx" : vin[3] == 'Y' ? "modely" : vin[3] == 'C' ? "cybertruck" : "";
            if (vinModel != model) return Missing("VIN and vehicle model do not agree.");
            var variants = Variants(model, trim, year.Value);
            if (variants.Length == 0) return Missing(identity + ": version code has no verified mapping.");
            var size = Regex.Match(wheel, "(?:18|19|20|21|22)");
            if (!size.Success) return Missing(identity + ": wheel configuration is missing or unrecognized.");
            var matches = rows.OfType<JObject>().Where(row =>
            {
                if ((int?)row["year"] != year.Value) return false;
                string name = (string)row["model"] ?? "";
                var wheelSize = Regex.Match(name, @"(18|19|20|21|22)\s*in", RegexOptions.IgnoreCase);
                if (wheelSize.Success && wheelSize.Groups[1].Value != size.Value) return false;
                // Strip wheel descriptors, retaining the old battery-pack descriptors.
                name = Regex.Replace(name, @"\((18|19|20|21|22).*?\)", "", RegexOptions.IgnoreCase);
                string normalized = Normalize(name);
                if (!normalized.StartsWith(model, StringComparison.Ordinal)) return false;
                string variant = normalized.Substring(model.Length);
                variant = variant.Replace("kwhrbatterypack", "").Replace("kwh", "");
                if (model == "models" || model == "modelx")
                {
                    if (variant.StartsWith("awd"))
                    {
                        variant = variant.Substring(3);
                        if (Regex.IsMatch(variant, "^[0-9]+$")) variant += "d";
                    }
                }
                return variants.Contains(variant) && (double?)row["rangeMiles"] > 0;
            }).ToArray();
            if (matches.Length != 1)
                return Missing(identity + (matches.Length > 1 ? ": multiple reference configurations match; no value selected." : ": no verified reference for this configuration."));
            var selected = matches[0];
            return new Match
            {
                Kilometers = (double)selected["rangeMiles"] * 1.609344,
                Description = "EPA reference · " + year.Value + " " + (string)selected["model"] + ". May differ from original displayed range.",
                SourceUrl = "https://www.fueleconomy.gov/feg/Find.do?action=sbs&id=" + (string)selected["epaId"]
            };
        }
        private static Match FindChina(string vin, JObject config, int year, string model, string trim, string wheel, JArray rows)
        {
            if (rows == null) return Missing("China reference catalog is unavailable.");
            if ((vin[3] == '3' ? "model3" : vin[3] == 'Y' ? "modely" : "") != model || (bool?)config["rhd"] != false)
                return Missing("Vehicle identity does not match a supported China configuration.");
            var matches = rows.OfType<JObject>().Where(row =>
                Normalize((string)row["model"]) == model
                && row["years"].Values<int>().Contains(year)
                && row["trims"].Values<string>().Select(Normalize).Contains(trim)
                && row["wheels"].Values<string>().Select(Normalize).Contains(Normalize(wheel))
                && (row["chemistryCode"] == null || row["chemistryCode"].Type == JTokenType.Null || (string)row["chemistryCode"] == vin[6].ToString())).ToArray();
            if (matches.Length != 1)
                return Missing("China configuration: " + year + " " + model + " / " + trim + " / " + wheel + ". No unambiguous community reference yet.");
            var selected = matches[0];
            double min = (double)selected["minKm"], max = (double)selected["maxKm"];
            if (min <= 0 || max < min || max > 2000) return Missing("Invalid China reference data.");
            return new Match
            {
                Kilometers = min == max ? min : (double?)null,
                MinimumKm = min, MaximumKm = max, Basis = "CN community",
                Description = "China owner-reported reference · " + year + " " + model + ". " + (string)selected["note"],
                SourceUrl = (string)selected["source"]
            };
        }
        private static Match Missing(string reason) => new Match { Description = reason };
        private static string[] Variants(string model, string trim, int year)
        {
            if (model == "models" || model == "modelx")
            {
                if (year <= 2018 && Regex.IsMatch(trim, "^p?(40|60|70|75|85|90|100)d?$")) return new[] { trim };
                if (trim == "plaid" || trim == "longrange" || trim == "longrangeplus" || trim == "standardrange" || trim == "performance") return new[] { trim };
                return new string[0];
            }
            if (model == "model3" || model == "modely")
            {
                // These are candidates, not globally unique trim IDs. Multiple matches are rejected.
                if (trim == "50") return new[] { "standardrange", "standardrangeplus", "standardrangeplusrwd", "standardrwd", "rwd", "standardrangerwd" };
                if (trim == "74") return new[] { "longrange", "longrangerwd", "premiumrwd", "longrangerwdi" };
                if (trim == "74d") return new[] { "longrangeawd", "longrangeawde", "longrangeawdi", "premiumawd" };
                if (trim == "p74d") return new[] { "longrangeawdperformance", "longrangeperformanceawd", "performanceawd", "performance" };
                if (trim == "62") return new[] { "midrange" };
            }
            // Unknown badges (including Cybertruck numeric badges) must not guess a trim.
            return new string[0];
        }
    }
}
