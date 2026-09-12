using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TeslaMurphy.Models;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace TeslaMurphy.Services
{
    internal static class TeslaConfiguration
    {
        private sealed class Credentials
        {
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
            public string CommandBaseUrl { get; set; }
        }

        private static readonly Lazy<Dictionary<string, Credentials>> credentials =
            new Lazy<Dictionary<string, Credentials>>(() =>
            {
                using (var stream = typeof(TeslaConfiguration).GetTypeInfo().Assembly
                    .GetManifestResourceStream("TeslaMurphy.TeslaCredentials.json"))
                {
                    if (stream == null) return new Dictionary<string, Credentials>();
                    using (var reader = new StreamReader(stream))
                        return JsonConvert.DeserializeObject<Dictionary<string, Credentials>>(reader.ReadToEnd())
                            ?? new Dictionary<string, Credentials>();
                }
            });

        public static string AuthorizeUrl => "https://auth." + AppSettings.Instance.Region_URL + "/oauth2/v3/authorize";
        public static string TokenUrl => "https://auth." + AppSettings.Instance.Region_URL + "/oauth2/v3/token";
        public static string CommandBaseUrl
        {
            get
            {
                string code = AppSettings.Instance.Region == 2 ? "CN" : AppSettings.Instance.Region == 1 ? "EU" : "NA";
                credentials.Value.TryGetValue(code, out var value);
                if (string.IsNullOrWhiteSpace(value?.CommandBaseUrl)) return AppSettings.Instance.Base_URL;
                if (!Uri.TryCreate(value.CommandBaseUrl, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
                    !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                    throw new InvalidOperationException("CommandBaseUrl must be an HTTPS URL.");
                return value.CommandBaseUrl.TrimEnd('/');
            }
        }
        public const string RedirectUri = "http://localhost:8000/callback";
        public const string Scope = "openid offline_access user_data vehicle_device_data vehicle_location vehicle_cmds vehicle_charging_cmds";

        public static void ApplyRegion(AppSettings settings, string regionCode)
        {
            string code = (regionCode ?? "NA").ToUpperInvariant();
            switch (code)
            {
                case "EU":
                    settings.Region = 1;
                    settings.Region_URL = "tesla.com";
                    settings.Base_URL = "https://fleet-api.prd.eu.vn.cloud.tesla.com";
                    break;
                case "CN":
                    settings.Region = 2;
                    settings.Region_URL = "tesla.cn";
                    settings.Base_URL = "https://fleet-api.prd.cn.vn.cloud.tesla.cn";
                    break;
                default:
                    code = "NA";
                    settings.Region = 0;
                    settings.Region_URL = "tesla.com";
                    settings.Base_URL = "https://fleet-api.prd.na.vn.cloud.tesla.com";
                    break;
            }
            credentials.Value.TryGetValue(code, out var value);
            settings.Client_id = value?.ClientId ?? "";
            settings.Client_secret = value?.ClientSecret ?? "";
        }

        public static async Task<bool> SelectLoginRegionAsync(int region)
        {
            string code = region == 2 ? "CN" : region == 1 ? "EU" : "NA";
            ApplyRegion(AppSettings.Instance, code);
            if (string.IsNullOrWhiteSpace(AppSettings.Instance.Client_id) ||
                string.IsNullOrWhiteSpace(AppSettings.Instance.Client_secret))
            {
                await new ContentDialog
                {
                    Title = "Tesla API configuration missing",
                    Content = "Configure ClientId and ClientSecret for " + code +
                        " in TeslaCredentials.local.json, then rebuild the app.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return false;
            }
            ApplicationData.Current.LocalSettings.Values["region"] = code;
            return true;
        }
    }
}
