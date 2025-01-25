using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using TeslaMurphy.Models;
using TeslaMurphy.Helpers;
using TeslaMurphy.ViewModels;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Services.Maps;
using Windows.UI.ViewManagement;
using Windows.UI;
using muxc = Microsoft.UI.Xaml.Controls;
using TeslaMurphy.Controls;

namespace TeslaMurphy.Views
{
    public sealed partial class CarPage : Page
    {
        private CarPageViewModel ViewModel => DataContext as CarPageViewModel;
        public CarPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
#if DEBUG
            VinTextBlock.Text = "DEBUG7FA1MA193314";
#endif

            if (AppSettings.Instance.CurrentTheme == ThemeMode.Dark)
                ViewModel.MapColorScheme = MapColorScheme.Dark;
            else
                ViewModel.MapColorScheme = MapColorScheme.Light;

            string code = e.Parameter as string;
            if (!string.IsNullOrEmpty(code))
            {
                code = code.Substring(code.IndexOf("code") + 5);
                //NetworkInfoTextBlock.Text = "parsing" + code;
                RemoveUserSettings();
                bool isLogedin = await HandleCallback(new Uri("teslauwp://localhost:8000/?code="+code));
                if (isLogedin)
                {
                    await Task.Delay(1000);
                    await Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync(string.Empty);
                    MainPage.Instance.ShowToast("Signed in, please reopen the app", null, 20);
                }
            }
            else
            {
                if (!AppSettings.Instance.IsTestMode && string.IsNullOrEmpty(AppSettings.Instance.Access_token))
                {
                    //Navigate user to login the app
                    if (ViewModel != null)
                    {
                        string loginUrl = await ViewModel.ShowLoginDialog();
                        if (loginUrl != null)
                        {
                            //await UWPGeneralHelper.OpenNewWindowAsync(typeof(Webview2Page), loginUrl);
                            Frame.Navigate(typeof(Webview2Page), loginUrl);
                        }
                    }
                }
                if (ViewModel?.CarData == null)
                {
                    ViewModel?.Intitalize(AppSettings.Instance.Current_carvin);
                    //await LoadNerbyChargingSites(AppSettings.Instance.Current_carvin);
                    //if (ViewModel.MapIcons.Count > 0) DestinationComboBox.Visibility = Visibility.Visible;
                    //else DestinationComboBox.Visibility = Visibility.Collapsed;
                }
            }

            if (AppSettings.Instance.IsTestMode)
                MainPage.Instance.ShowToast("Experience Mode", "\uF427");
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppSettings.Instance.Current_carvin != null)
            {
                DestinationComboBox.SelectedIndex = -1;
                await ViewModel.Intitalize(AppSettings.Instance.Current_carvin, true);
                await LoadNerbyChargingSites(AppSettings.Instance.Current_carvin);
                if (ViewModel.MapIcons.Count > 0) DestinationComboBox.Visibility = Visibility.Visible;
                else DestinationComboBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                //Should load main car vin
                ViewModel?.GetCarListAsync();
            }
        }

        private void ChargeInfoAppBarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ChargeInfoAppBarToggleButton.IsChecked == true)
                ChargeInfoStackPanel.Visibility = Visibility.Visible;
            else
                ChargeInfoStackPanel.Visibility = Visibility.Collapsed;
        }

        private async void ReleaseNotesHyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CarData != null)
            {
                if (AppSettings.Instance.IsPro)
                {
                    string releaseNotes = await ViewModel.GetReleaseNotesAsync(ViewModel.CarData.id.ToString());
                    if (!string.IsNullOrEmpty(releaseNotes))
                    {
                        await ViewModel.ShowReleaseNotes(releaseNotes);
                    }
                }
                else
                    DisplayPurchaseInfoAsync();
            }
        }

        private async void ACAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ACAppBarButton.IsChecked == false)
                {
                    bool isTurnedOff = await ViewModel.TurnOffACAsync(ViewModel.CarData.id.ToString());
                    if (!isTurnedOff)
                    {
                        ACAppBarButton.IsChecked = true;
                        MainPage.Instance.ShowToast("Failed, car-control coming soon");
                    }
                    else
                    {
                        ACAppBarButton.IsEnabled = false;
                        ViewModel.CarData.climate_state.is_climate_on = false;
                    }
                }
                else
                {
                    // Turn on AC
                }
            }
            catch
            {

            }
        }

        public async Task LoadNerbyChargingSites(string vehicle_tag)
        {
            //var MyLandmarks = new List<MapElement>();

            //BasicGeoposition snPosition = new BasicGeoposition { Latitude = 47.620, Longitude = -122.349 };
            //Geopoint snPoint = new Geopoint(snPosition);

            //var spaceNeedleIcon = new MapIcon
            //{
            //    Location = snPoint,
            //    NormalizedAnchorPoint = new Point(0.5, 1.0),
            //    ZIndex = 0,
            //    Title = "Space Needle"
            //};

            //MyLandmarks.Add(spaceNeedleIcon);

            //var LandmarksLayer = new MapElementsLayer
            //{
            //    ZIndex = 1,
            //    MapElements = MyLandmarks
            //};

            //TeslaChargingMap.Layers.Add(LandmarksLayer);

            //TeslaChargingMap.Center = snPoint;
            //TeslaChargingMap.ZoomLevel = 14;
            await ViewModel.GetNearbyChargingSitesAsync(vehicle_tag);
            if(ViewModel.MapIcons.Count != 0)
            {
                // add the map icons to the map
                foreach (var mapIcon in ViewModel.MapIcons)
                {
                    TeslaChargingMap.MapElements.Add(mapIcon);
                }
                TeslaChargingMap.Center = ViewModel.MapIcons.LastOrDefault().Location;
                TeslaChargingMap.ZoomLevel = 14;
            }
        }

        private void LocationButton_Click(object sender, RoutedEventArgs e)
        {
            CarInfoGrid.Visibility = Visibility.Collapsed;
            MapGrid.Visibility = Visibility.Visible;
            HideMapGridButton.Visibility = Visibility.Visible;
        }

        private void HideMapGridButton_Click(object sender, RoutedEventArgs e)
        {
            CarInfoGrid.Visibility = Visibility.Visible;
            MapGrid.Visibility = Visibility.Collapsed;
            HideMapGridButton.Visibility = Visibility.Collapsed;
        }

        private async void GetChargerSitesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AppSettings.Instance.IsPro)
                {
                    DestinationComboBox.SelectedIndex = -1;
                    await LoadNerbyChargingSites(ViewModel.CarData.vin);
                    if (ViewModel.MapIcons.Count > 0) DestinationComboBox.Visibility = Visibility.Visible;
                    else DestinationComboBox.Visibility = Visibility.Collapsed;
                }
                else
                    DisplayPurchaseInfoAsync();
            }
            catch
            {

            }
        }

        private async void TopRigtInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if(ViewModel?.CarData != null)
            {
                if (BatteryPercentageDanweiTextBlock.Text.Contains("%"))
                {

                    double miles = ViewModel.CarData.charge_state.ideal_battery_range;
                    string milesString = " miles";
                    if (AppSettings.Instance.Length_unit == 0)
                    {
                        miles = await CalculateHelpers.MilesToKM(miles);
                        milesString = " km";
                    }
                    BatteryPercentageTextBlock.Text = Math.Round(miles, 2).ToString("F2");
                    BatteryPercentageDanweiTextBlock.Text = milesString;//ViewModel.CarData.gui_settings.;
                }
                else
                {
                    BatteryPercentageTextBlock.Text = ViewModel.CarData.charge_state.battery_level.ToString();
                    BatteryPercentageDanweiTextBlock.Text = "%";
                }
            }
        }

        private async void DriversButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CarData != null)
            {
                string drivers = await ViewModel.GetDriversAsync(ViewModel.CarData.id.ToString());
                if (!string.IsNullOrEmpty(drivers))
                {
                    await ViewModel.ShowDrivers(drivers);
                }
            }
        }

        private async void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CarData != null)
            {
                if (AppSettings.Instance.IsPro)
                    await ViewModel.ShowSchedule(ViewModel.CarData.charge_state);
                else
                    DisplayPurchaseInfoAsync();
            }
        }

        private async void ServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CarData != null)
            {
                string service = "";
                if (ViewModel.CarData.in_service)
                    service = await ViewModel.GetSerivceAsync(ViewModel.CarData.vin);
                await ViewModel.ShowService(service);
            }
        }

        private async void ClimateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CarData != null)
            {
                if (AppSettings.Instance.IsPro)
                    await ViewModel.ShowClimate(ViewModel.CarData.climate_state);
                else
                    DisplayPurchaseInfoAsync();
            }
        }

        public async Task NavigateBetweenLocationsAsync(MapControl mapControl, BasicGeoposition startLocation, BasicGeoposition endLocation)
        {
            // 1. 创建起点和终点的地理位置
            Geopoint startPoint = new Geopoint(startLocation);
            Geopoint endPoint = new Geopoint(endLocation);

            // 2. 使用 MapRouteFinder 查找两点之间的路径
            MapRouteFinderResult routeResult = await MapRouteFinder.GetDrivingRouteAsync(
                startPoint,
                endPoint,
                MapRouteOptimization.Time, // 你可以选择时间最短或距离最短
                MapRouteRestrictions.None   // 你可以指定一些路径限制，例如避免高速公路等
            );

            if (routeResult.Status == MapRouteFinderStatus.Success)
            {
                var uiSettings = new UISettings();
                Color accentColor = uiSettings.GetColorValue(UIColorType.Accent);
                // 3. 绘制路线
                MapRouteView routeView = new MapRouteView(routeResult.Route)
                {
                    RouteColor = accentColor,  // 设置路线的颜色
                    OutlineColor = Windows.UI.Colors.Black
                };

                // 4. 将路线添加到MapControl
                mapControl.Routes.Add(routeView);

                // 5. 调整 MapControl 的视图以显示整个路线
                await mapControl.TrySetViewBoundsAsync(
                    routeResult.Route.BoundingBox,
                    null,
                    MapAnimationKind.Bow);  // 设置显示动画

                // 6. 可选：添加起点和终点的图标（针）
                var startIcon = new MapIcon
                {
                    Location = startPoint,
                    Title = "Start",
                    NormalizedAnchorPoint = new Windows.Foundation.Point(0.5, 1.0),
                    ZIndex = 0
                };
                var endIcon = new MapIcon
                {
                    Location = endPoint,
                    Title = "End",
                    NormalizedAnchorPoint = new Windows.Foundation.Point(0.5, 1.0),
                    ZIndex = 0
                };

                mapControl.MapElements.Add(startIcon);
                mapControl.MapElements.Add(endIcon);
            }
            else
            {
                // 处理找不到路径的情况
                //await new Windows.UI.Popups.MessageDialog("No route found").ShowAsync();
                await DisplayPopout.dualButton("Sorry", "No route found", "Ok", "Cancel");
            }
        }

        private void UserLocationTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if(e.Key == Windows.System.VirtualKey.Enter)
            {
                if(UserLocationTextBox.Text != null)
                {
                    ViewModel?.SearchUserLocationAsync(UserLocationTextBox.Text);
                }
            }
        }

        private  async void UserLocationsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var mapLocation = (MapLocation)e.ClickedItem;
            if(mapLocation != null)
            {
                BasicGeoposition startLocation = new BasicGeoposition { Latitude = mapLocation.Point.Position.Latitude, Longitude = mapLocation.Point.Position.Longitude };
                UserLocationTextBox.Text = mapLocation.DisplayName;
                if (DestinationComboBox.SelectedIndex > -1)
                {
                    var mapIcon = ViewModel.MapIcons[DestinationComboBox.SelectedIndex];
                    BasicGeoposition endLocation = new BasicGeoposition { Latitude = mapIcon.Location.Position.Latitude, Longitude = mapIcon.Location.Position.Longitude };
                    await NavigateBetweenLocationsAsync(TeslaChargingMap, startLocation, endLocation);
                    ViewModel.UserMapLocations.Clear();
                }
                else
                {
                    var geopoint = new Geopoint(new BasicGeoposition
                    {
                        Latitude =mapLocation.Point.Position.Latitude,
                        Longitude = mapLocation.Point.Position.Longitude
                    });
                    var mapIcon = new MapIcon
                    {
                        Location = geopoint,
                        Title = mapLocation.DisplayName,
                        ZIndex = 0
                    };
                    TeslaChargingMap.MapElements.Add(mapIcon);
                    TeslaChargingMap.Center = geopoint;
                    TeslaChargingMap.ZoomLevel = 14;
                }
            }
        }

        private async void DestinationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (UserLocationsListView.SelectedIndex > -1 && DestinationComboBox.SelectedIndex > -1)
                {
                    var mapLocation = ViewModel.UserMapLocations[UserLocationsListView.SelectedIndex];
                    BasicGeoposition startLocation = new BasicGeoposition { Latitude = mapLocation.Point.Position.Latitude, Longitude = mapLocation.Point.Position.Longitude };
                    var mapIcon = ViewModel.MapIcons[DestinationComboBox.SelectedIndex];
                    BasicGeoposition endLocation = new BasicGeoposition { Latitude = mapIcon.Location.Position.Latitude, Longitude = mapIcon.Location.Position.Longitude };
                    await NavigateBetweenLocationsAsync(TeslaChargingMap, startLocation, endLocation);

                    ViewModel.UserMapLocations.Clear();
                }
                else
                {
                    var mapIcon = ViewModel.MapIcons[DestinationComboBox.SelectedIndex];
                    TeslaChargingMap.MapElements.Add(mapIcon);
                    TeslaChargingMap.Center = mapIcon.Location;
                    TeslaChargingMap.ZoomLevel = 14;
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// Authentication Code
        /// </summary>
        /// <param name="callbackUri"></param>
        /// <returns></returns>
        private async Task<bool> HandleCallback(Uri callbackUri)
        {
            try
            {
                var queryParams = callbackUri.Query.TrimStart('?').Split('&');
                string token = null;
                //Debug
                //AuthorizeTextBlock.Text = queryParams.ToString();
                foreach (var param in queryParams)
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2 && parts[0] == "code")
                    {
                        token = parts[1];
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(token))
                {
                    bool isSucceed = await ExchangeTokenForSession(token);
                    if (isSucceed)
                        return true;
                    else
                        return false;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExchangeTokenForSession(string token)
        {
            try
            {
                string teslaCodeExchangeURL = "https://auth." + AppSettings.Instance.Region_URL + "/oauth2/v3/token";
                // Exchange token for session key
                var postData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("redirect_uri", "http://localhost:8000/callback"),
                    new KeyValuePair<string, string>("client_id", AppSettings.Instance.Client_id),
                    new KeyValuePair<string, string>("client_secret", AppSettings.Instance.Client_secret),
                    new KeyValuePair<string, string>("audience", AppSettings.Instance.Base_URL),
                    new KeyValuePair<string, string>("scope", "openid offline_access user_data vehicle_device_data vehicle_cmds vehicle_charging_cmds"),
                    new KeyValuePair<string, string>("code", token) // This is the authorization code you received
                });

                using (var client = new HttpClient())
                {
                    var response = await client.PostAsync(teslaCodeExchangeURL, postData);
                    var content = await response.Content.ReadAsStringAsync();
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(content);
                    string access_token = result.access_token;
                    string refresh_token = result.refresh_token;
                    // Save the session key securely for future use
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["refreshtoken"] = refresh_token;
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["accesstoken"] = access_token;
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["accesstokentime"] = DateTime.Now.Date.ToString();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private async void CarDropDownButton_Click(object sender, RoutedEventArgs e)
        {
            // Load car list into menu flyout
            try
            {
                if (CarMenuFlyout.Items.Count == 0)
                {
                    var carlist = await ViewModel.GetCarListAsync();
                    // 
                    foreach (var car in carlist)
                    {
                        muxc.RadioMenuFlyoutItem rmfi = new muxc.RadioMenuFlyoutItem();
                        rmfi.Tag = car.vin;
                        if (rmfi.Text != car.display_name)
                            rmfi.Text = car.display_name;
                        else
                            rmfi.Text = car.vin;
                        rmfi.Click += SwitchMainCarMfi_Click;

                        if(AppSettings.Instance.Current_carvin == car.vin)
                            rmfi.IsChecked = true;

                        rmfi.GroupName = "CarList";

                        CarMenuFlyout.Items.Add(rmfi);
                    }
                }
            }
            catch
            {

            }
        }

        private async void SwitchMainCarMfi_Click(object sender, RoutedEventArgs e)
        {
            var rmfi = (muxc.RadioMenuFlyoutItem)sender;
            if(rmfi.Tag != null)
            {
                if (AppSettings.Instance.IsPro)
                {
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["maincarvin"] = rmfi.Tag.ToString();
                    AppSettings.Instance.Current_carvin = rmfi.Tag.ToString();
                    await ViewModel.Intitalize(rmfi.Tag.ToString());
                }
                else
                    DisplayPurchaseInfoAsync();
            }
        }

        private void RemoveUserSettings()
        {
            try
            {
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values.Remove("maincarvin");
                localSettings.Values.Remove("refreshtoken");
                localSettings.Values.Remove("accesstoken");
            }
            catch
            {

            }
        }

        private void RecentAlertsButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void DisplayPurchaseInfoAsync()
        {
            PurchaseProContentDialog dialog = new PurchaseProContentDialog();
            dialog.DefaultButton = ContentDialogButton.Primary;
            ContentDialogResult result = await dialog.ShowAsync();
        }

        private void CloseAds_Click(object sender, RoutedEventArgs e)
        {
            //DisplayPurchaseInfo();
        }

        private async void LocalAdsBlock_Click(object sender, RoutedEventArgs e)
        {
            Uri targetUri = new Uri(String.Format("{0}", AdsSubTitleTextBlock.Tag, UriKind.Relative));
            var options = new Windows.System.LauncherOptions();
            options.DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.UseHalf;
            await Windows.System.Launcher.LaunchUriAsync(targetUri, options);
        }

        //private async Task<bool> GeneratePartnerToken(string token)
        //{
        //    // Exchange token for session key
        //    try
        //    {
        //        //美区账号初始化要注册下Partner Token
        //        //Initiate the Partner Token for US account
        //        string teslaCodeExchangeURL = "https://auth.tesla.cn/oauth2/v3/token";
        //        // 构造请求参数
        //        var postData = new FormUrlEncodedContent(new[]
        //        {
        //            new KeyValuePair<string, string>("client_id", ""), // Your client ID
        //            new KeyValuePair<string, string>("redirect_uri", "teslauwp://localhost:8000/callback"),
        //            new KeyValuePair<string, string>("client_secret", ""),
        //            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        //            new KeyValuePair<string, string>("audience", "https://fleet-api.prd.cn.vn.cloud.tesla.cn"),
        //            new KeyValuePair<string, string>("scope", "openid offline_access user_data vehicle_device_data vehicle_cmds vehicle_charging_cmds"),
        //            new KeyValuePair<string, string>("code", token) // This is the authorization code you received
        //        });

        //        using (var client = new HttpClient())
        //        {
        //            var response = await client.PostAsync(teslaCodeExchangeURL, postData);
        //            var content = await response.Content.ReadAsStringAsync();
        //            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(content);
        //            //Debug
        //            AuthorizeTextBlock.Text = result;
        //            string access_token = result.access_token;
        //            string refresh_token = result.refresh_token;
        //            // Save the session key securely for future use
        //            Windows.Storage.ApplicationData.Current.LocalSettings.Values["refreshtoken"] = refresh_token;
        //            Windows.Storage.ApplicationData.Current.LocalSettings.Values["accesstoken"] = access_token;
        //            return true;
        //        }
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        //private async void AuthorizeButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!AuthorizeTextBlock.Text.Contains("CN_"))
        //    {
        //        AuthorizeTextBlock.Text = "Authenticating...";
        //        PB.IsIndeterminate = true;
        //        string authRequestUrl = await TeslaFleetServices.GenerateAuthorizeUriAsync("https://auth." + AppSettings.Instance.Region_URL + "/oauth2/v3/authorize", AppSettings.Instance.Client_id);
        //        UWPGeneralHelper.OpenInDefaultBrowser(authRequestUrl);
        //    }
        //    else
        //    {
        //        HandleCallback(new Uri("teslauwp://localhost:8000/?code=" + AuthorizeTextBlock.Text));
        //    }
        //}
    }
}