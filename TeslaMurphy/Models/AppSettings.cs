using CommunityToolkit.Mvvm.ComponentModel;

namespace TeslaMurphy.Models
{
    public partial class AppSettings : ObservableObject
    {
        private static AppSettings instance;

        public static AppSettings Instance => instance ??= new AppSettings();

        private AppSettings()
        {
            isLogedin = false;
            //access_token = "";
            Base_URL = "https://fleet-api.prd.na.vn.cloud.tesla.com";
            region_URL = "tesla.com";
            client_id = "";
            client_secret = "";
            length_unit = 0; // 0 = km, 1 = miles
            IsPro = false;
            isTestMode = false;
            Device = "Desktop";
        }

        [ObservableProperty]
        private string base_URL;

        [ObservableProperty]
        private string current_carvin;

        [ObservableProperty]
        private string access_token;

        [ObservableProperty]
        private string refresh_token;

        [ObservableProperty]
        private bool isLogedin;

        [ObservableProperty]
        private bool isPro;

        [ObservableProperty]
        private string device;

        [ObservableProperty]
        private ThemeMode currentTheme;

        [ObservableProperty]
        private bool isTestMode;

        [ObservableProperty]
        private string client_id;

        [ObservableProperty]
        private string client_secret;

        [ObservableProperty]
        private int length_unit;

        [ObservableProperty]
        private int region; // NA 0 EU 1 CN 2

        [ObservableProperty]
        private string region_URL;
    }
}
