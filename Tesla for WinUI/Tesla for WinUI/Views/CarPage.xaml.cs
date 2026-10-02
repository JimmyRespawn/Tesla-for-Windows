using MapIcon = TeslaMurphy.Controls.Maps.MapIcon;
using TeslaMurphy.Controls.Maps;
using System;
using System.Linq;
using TeslaMurphy.Services;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TeslaMurphy.Models;
using TeslaMurphy.Helpers;
using TeslaMurphy.ViewModels;
using Windows.Devices.Geolocation;

using Muxc = Microsoft.UI.Xaml.Controls;
using TeslaMurphy.Controls;

namespace TeslaMurphy.Views
{
    public sealed partial class CarPage : Page
    {
        private CarPageViewModel ViewModel => DataContext as CarPageViewModel;
        private Geopoint vehicleMapLocation;
        private string loadedMapVin;
        private int mapLoadRevision;
        private bool openingAmapRoute;

        public CarPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            ActualThemeChanged += (s, e) => ApplyMapTheme();
            var mapResources = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            var centerLabel = mapResources.GetString("MapCenterVehicle");
            var refreshLabel = mapResources.GetString("MapRefreshChargingSites");
            ToolTipService.SetToolTip(CenterVehicleButton, centerLabel);
            ToolTipService.SetToolTip(GetChargerSitesButton, refreshLabel);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CenterVehicleButton, centerLabel);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(GetChargerSitesButton, refreshLabel);
            Loaded += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.PropertyChanged -= ChargeLimitModelChanged;
                    ViewModel.PropertyChanged += ChargeLimitModelChanged;
                }
                UpdateChargeLimitApplyVisibility();
                ApplyMapTheme();
            };
            Unloaded += (s, e) => { if (ViewModel != null) ViewModel.PropertyChanged -= ChargeLimitModelChanged; };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await DesktopHelper.WaitForLoadedAsync(this);

            ApplyMapTheme();

            string code = e.Parameter as string;
            if (!string.IsNullOrEmpty(code))
            {

                //NetworkInfoTextBlock.Text = "parsing" + code;
                RemoveUserSettings();
                bool isLogedin = await HandleCallback(new Uri("teslauwp://localhost:8000/?" + code));
                if (isLogedin)
                {
                    await Task.Delay(1000);
                    await TeslaMurphy.Helpers.DesktopHelper.RestartAsync();
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
                if (ViewModel != null)
                {
                    string vin = AppSettings.Instance.Current_carvin;
                    var previousData = ViewModel.CarData;
                    long refreshRevision = AppSettings.Instance.IsTestMode ? 0 : VehicleDataRefreshTracker.GetPendingRevision(vin);
                    bool refreshRequired = refreshRevision != 0;
                    if (previousData == null || previousData.vin != vin)
                        await ViewModel.Intitalize(vin, refreshRequired);
                    else if (refreshRequired)
                        await ViewModel.GetCarInfoAsync(vin, true);
                    // Failed reads retain the previous snapshot and must retain the refresh marker too.
                    if (refreshRequired && AppSettings.Instance.Current_carvin == vin
                        && ViewModel.CarData?.vin == vin && !ReferenceEquals(previousData, ViewModel.CarData))
                        VehicleDataRefreshTracker.MarkRefreshed(vin, refreshRevision);
                    if (ViewModel.CarData != null && loadedMapVin != ViewModel.CarData.vin)
                        await LoadNerbyChargingSites(ViewModel.CarData.vin);
                }
            }

            if (AppSettings.Instance.IsTestMode)
                MainPage.Instance.ShowToast("Experience Mode", "\uF427");
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppSettings.Instance.Current_carvin != null)
            {
                string vin = AppSettings.Instance.Current_carvin;
                DestinationComboBox.SelectedIndex = -1;
                await ViewModel.Intitalize(vin, true);
                if (AppSettings.Instance.Current_carvin != vin) return;
                await LoadNerbyChargingSites(vin);
                if (ViewModel.MapIcons?.Count > 0) DestinationComboBox.Visibility = Visibility.Visible;
                else DestinationComboBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                //Should load main car vin
                if (ViewModel != null) await ViewModel.Intitalize(null);
            }
        }

        private void ChargeInfoAppBarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ChargeInfoAppBarToggleButton.IsChecked == true)
                ChargeInfoStackPanel.Visibility = Visibility.Visible;
            else
                ChargeInfoStackPanel.Visibility = Visibility.Collapsed;
        }

        private async void VehicleSoftware_Click(object sender, RoutedEventArgs e)
        {
            var car = ViewModel?.CarData;
            if (car == null || string.IsNullOrWhiteSpace(car.vin)) return;
            var dialog = new VehicleHealthContentDialog { VehicleVin = car.vin, VehicleName = car.vehicle_state?.vehicle_name, VehicleSnapshot = Newtonsoft.Json.Linq.JObject.FromObject(car) };
            await TeslaMurphy.Helpers.DesktopHelper.ShowDialogAsync(dialog);
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

        private bool vehicleCommandInProgress;

        private void UpdateChargeLimitApplyVisibility()
        {
            if (ApplyChargeLimitButton == null || ChargeLimitSlider == null) return;
            var state = ViewModel?.CarData?.charge_state;
            bool changed = state != null && !AppSettings.Instance.IsTestMode
                && (int)Math.Round(ChargeLimitSlider.Value) != state.charge_limit_soc;
            ApplyChargeLimitButton.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChargeLimitSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
            => UpdateChargeLimitApplyVisibility();

        private async void ChargeLimitModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CarPageViewModel.CarData))
                await TeslaMurphy.Helpers.DesktopHelper.RunAsync(DispatcherQueue, UpdateChargeLimitApplyVisibility);
        }

        private void RestoreChargeLimitBinding()
        {
            ChargeLimitSlider.SetBinding(Microsoft.UI.Xaml.Controls.Primitives.RangeBase.ValueProperty,
                new Microsoft.UI.Xaml.Data.Binding
                {
                    Path = new PropertyPath("CarData.charge_state.charge_limit_soc"),
                    Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay,
                    FallbackValue = 0, TargetNullValue = 0
                });
            UpdateChargeLimitApplyVisibility();
        }

        private async void ApplyChargeLimitButton_Click(object sender, RoutedEventArgs e)
        {
            if (vehicleCommandInProgress) return;
            var car = ViewModel.CarData;
            if (car?.charge_state == null || string.IsNullOrWhiteSpace(car.vin) || AppSettings.Instance.IsTestMode)
            {
                RestoreChargeLimitBinding();
                MainPage.Instance.ShowToast("Charge limit control requires a connected vehicle.");
                return;
            }
            int percent = (int)Math.Round(ChargeLimitSlider.Value);
            int minimum = Math.Max(0, car.charge_state.charge_limit_soc_min);
            int maximum = car.charge_state.charge_limit_soc_max;
            if (maximum <= 0 || maximum > 100) maximum = 100;
            if (percent < minimum || percent > maximum)
            {
                RestoreChargeLimitBinding();
                MainPage.Instance.ShowToast("Choose a charge limit between " + minimum + "% and " + maximum + "%.");
                return;
            }
            if (percent == car.charge_state.charge_limit_soc) return;
            vehicleCommandInProgress = true;
            ApplyChargeLimitButton.IsEnabled = ChargeLimitSlider.IsEnabled = false;
            ApplyChargeLimitButton.Content = "Applying…";
            try
            {
                if (await ViewModel.SetChargeLimitAsync(car.vin, percent))
                {
                    car.charge_state.charge_limit_soc = percent;
                    MainPage.Instance.ShowToast("Charge limit set to " + percent + "%.");
                }
                else MainPage.Instance.ShowToast(ViewModel.VehicleCommandError);
            }
            catch { MainPage.Instance.ShowToast("Unable to set charge limit. Please refresh and try again."); }
            finally
            {
                // Rebind to the currently selected vehicle, including after a mid-request switch.
                RestoreChargeLimitBinding();
                ApplyChargeLimitButton.Content = "Apply";
                ApplyChargeLimitButton.IsEnabled = ChargeLimitSlider.IsEnabled = true;
                vehicleCommandInProgress = false;
            }
        }

        private async void ChargePortButton_Click(object sender, RoutedEventArgs e)
        {
            if (vehicleCommandInProgress) return;
            var car = ViewModel.CarData;
            if (car?.charge_state == null || string.IsNullOrWhiteSpace(car.vin) || AppSettings.Instance.IsTestMode)
            {
                MainPage.Instance.ShowToast("Charge port control requires a connected vehicle.");
                return;
            }
            bool open = !car.charge_state.charge_port_door_open;
            string state = car.charge_state.charging_state;
            if (!open && (state == "Charging" || state == "Complete" || state == "Stopped"
                || state == "Starting" || state == "NoPower"))
            {
                MainPage.Instance.ShowToast("Unplug the charging cable before closing the charge port.");
                return;
            }
            vehicleCommandInProgress = true;
            ChargePortButton.IsEnabled = LockAppBarButton.IsEnabled = ACAppBarButton.IsEnabled = false;
            try
            {
                if (await ViewModel.SetChargePortAsync(car.vin, open))
                {
                    car.charge_state.charge_port_door_open = open;
                    if (ReferenceEquals(car, ViewModel.CarData))
                        ChargePortStateText.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding
                        {
                            Path = new PropertyPath("CarData.charge_state.charge_port_door_open"),
                            Converter = new TeslaMurphy.Converters.BooleanToOpenCloseConverter(),
                            ConverterParameter = "Door"
                        });
                    MainPage.Instance.ShowToast(open ? "Charge port opened." : "Charge port closed.");
                }
                else
                    MainPage.Instance.ShowToast(ViewModel.VehicleCommandError);
            }
            catch { MainPage.Instance.ShowToast("Unable to change the charge port. Please try again."); }
            finally
            {
                vehicleCommandInProgress = false;
                ChargePortButton.IsEnabled = LockAppBarButton.IsEnabled = ACAppBarButton.IsEnabled = true;
            }
        }

        private async void LockAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (vehicleCommandInProgress) return;
            var car = ViewModel.CarData;
            if (car?.vehicle_state == null || string.IsNullOrWhiteSpace(car.vin) || AppSettings.Instance.IsTestMode)
            {
                MainPage.Instance.ShowToast("Please connect your vehicle first. Demo mode does not support vehicle lock control.");
                return;
            }
            bool locked = !car.vehicle_state.locked;
            vehicleCommandInProgress = true;
            LockAppBarButton.IsEnabled = false;
            ACAppBarButton.IsEnabled = false;
            try
            {
                if (await ViewModel.SetDoorLockAsync(car.vin, locked))
                {
                    car.vehicle_state.locked = locked;
                    if (ReferenceEquals(car, ViewModel.CarData))
                    {
                        // Rebind because the vehicle state model does not notify property changes.
                        var icon = (FontIcon)LockAppBarButton.Icon;
                        icon.SetBinding(FontIcon.GlyphProperty, new Microsoft.UI.Xaml.Data.Binding
                        {
                            Path = new PropertyPath("CarData.vehicle_state.locked"),
                            Converter = new TeslaMurphy.Converters.LockStateToIconConverter()
                        });
                    }
                    MainPage.Instance.ShowToast(locked ? "Car Locked" : "Car Unlocked");
                }
                else
                    await new ContentDialog { XamlRoot = App.MainWindow.Content.XamlRoot, Title = locked ? "Failed to lock car" : "Failed to unlock car", Content = ViewModel.VehicleCommandError, CloseButtonText = "CLOSE" }.ShowAsync();
            }
            catch
            {
                MainPage.Instance.ShowToast("Car lock command failed. Please check the connection and try again.");
            }
            finally
            {
                vehicleCommandInProgress = false;
                LockAppBarButton.IsEnabled = true;
                ACAppBarButton.IsEnabled = true;
            }
        }
        private async void ACAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            var car = ViewModel.CarData;
            if (vehicleCommandInProgress) return;
            if (car?.climate_state == null || AppSettings.Instance.IsTestMode)
            {
                ACAppBarButton.IsChecked = car?.climate_state?.is_climate_on ?? false;
                MainPage.Instance.ShowToast("Climate control requires a connected vehicle.");
                return;
            }
            bool previousState = car.climate_state.is_climate_on;
            bool turnOn = ACAppBarButton.IsChecked == true;
            vehicleCommandInProgress = true;
            ACAppBarButton.IsEnabled = false;
            LockAppBarButton.IsEnabled = false;
            try
            {
                bool succeeded = turnOn
                    ? await ViewModel.TurnOnACAsync(car.vin)
                    : await ViewModel.TurnOffACAsync(car.vin);
                if (succeeded)
                    car.climate_state.is_climate_on = turnOn;
                else
                    await new ContentDialog { XamlRoot = App.MainWindow.Content.XamlRoot, Title = "空调指令未成功", Content = ViewModel.VehicleCommandError, CloseButtonText = "CLOSE" }.ShowAsync();
                if (ReferenceEquals(car, ViewModel.CarData))
                    ACAppBarButton.IsChecked = succeeded ? turnOn : previousState;
            }
            catch
            {
                if (ReferenceEquals(car, ViewModel.CarData)) ACAppBarButton.IsChecked = previousState;
                MainPage.Instance.ShowToast("Unable to change climate. Please try again.");
            }
            finally
            {
                vehicleCommandInProgress = false;
                ACAppBarButton.IsEnabled = true;
                LockAppBarButton.IsEnabled = true;
            }
        }
        public async Task LoadNerbyChargingSites(string vehicle_tag)
        {
            int revision = ++mapLoadRevision;
            var car = ViewModel.CarData;
            var sitesTask = ViewModel.GetNearbyChargingSitesAsync(vehicle_tag);
            var locationTask = ViewModel.GetVehicleLocationAsync(vehicle_tag);
            await Task.WhenAll(sitesTask, locationTask);
            bool sitesLoaded = await sitesTask;
            var location = await locationTask;
            if (revision != mapLoadRevision || !ReferenceEquals(car, ViewModel.CarData)
                || AppSettings.Instance.Current_carvin != vehicle_tag) return;
            DestinationComboBox.SelectedIndex = -1;
            loadedMapVin = sitesLoaded ? vehicle_tag : null;
            vehicleMapLocation = location;
            CenterVehicleButton.IsEnabled = location != null;
            UpdateAmapRouteAction();
            DestinationComboBox.Visibility = ViewModel.MapIcons?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TeslaChargingMap.MapElements.Clear();
            if (ViewModel.MapIcons != null)
                foreach (var icon in ViewModel.MapIcons) TeslaChargingMap.MapElements.Add(icon);
            if (location != null)
            {
                TeslaChargingMap.MapElements.Add(new MapIcon
                {
                    Location = location, Title = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("MapYourVehicle"),
                    ZIndex = 10, Kind = MapIconKind.Vehicle
                });
                TeslaChargingMap.Center = location;
                TeslaChargingMap.ZoomLevel = 13;
            }
            else
            {
                if (ViewModel.MapIcons?.Count > 0)
                {
                    TeslaChargingMap.Center = ViewModel.MapIcons.First().Location;
                    TeslaChargingMap.ZoomLevel = 13;
                }
                if (!string.IsNullOrEmpty(ViewModel.VehicleLocationError))
                    MainPage.Instance.ShowToast(ViewModel.VehicleLocationError);
            }
            if (!sitesLoaded) MainPage.Instance.ShowToast("Unable to load nearby charging sites.");
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
                    if (ViewModel.CarData == null) return;
                    GetChargerSitesButton.IsEnabled = false;
                    DestinationComboBox.SelectedIndex = -1;
                    await LoadNerbyChargingSites(ViewModel.CarData.vin);
                    if (ViewModel.MapIcons.Count > 0) DestinationComboBox.Visibility = Visibility.Visible;
                    else DestinationComboBox.Visibility = Visibility.Collapsed;
                }
                else
                    DisplayPurchaseInfoAsync();
            }
            catch { MainPage.Instance.ShowToast("Unable to load the map. Please try again."); }
            finally { GetChargerSitesButton.IsEnabled = true; }
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
                string vin = ViewModel.CarData.vin;
                await ViewModel.ShowDrivers(vin);
            }
        }

        private async void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CarData != null)
            {
                if (AppSettings.Instance.IsPro)
                {
                    if (vehicleCommandInProgress) return;
                    await ViewModel.ShowSchedule(ViewModel.CarData.charge_state);
                }
                else
                    DisplayPurchaseInfoAsync();
            }
        }

        private async void ServiceButton_Click(object sender, RoutedEventArgs e)
        {
            var car = ViewModel.CarData;
            if (car == null) return;
            ServiceButton.IsEnabled = false;
            try
            {
                string service = car.in_service ? await ViewModel.GetSerivceAsync(car.vin) : null;
                await ViewModel.ShowService(service, car.in_service, car.vehicle_state?.vehicle_name);
            }
            finally { ServiceButton.IsEnabled = true; }
        }

        private async void ClimateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CarData != null)
            {
                if (AppSettings.Instance.IsPro)
                {
                    if (vehicleCommandInProgress) return;
                    await ViewModel.ShowClimate(ViewModel.CarData.climate_state);
                    ACAppBarButton.IsChecked = ViewModel.CarData?.climate_state?.is_climate_on ?? false;
                }
                else
                    DisplayPurchaseInfoAsync();
            }
        }

        private void ApplyMapTheme()
        {
            if (ViewModel != null)
                ViewModel.MapColorScheme = ActualTheme == ElementTheme.Dark ? MapColorScheme.Dark : MapColorScheme.Light;
        }

        private void CenterVehicleButton_Click(object sender, RoutedEventArgs e)
        {
            if (vehicleMapLocation == null) return;
            DestinationComboBox.SelectedIndex = -1;
            TeslaChargingMap.Center = vehicleMapLocation;
            TeslaChargingMap.ZoomLevel = 14;
        }

        private void DestinationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAmapRouteAction();
            if (DestinationComboBox.SelectedItem is not MapIcon { Location: not null } icon) return;
            TeslaChargingMap.Center = icon.Location;
            TeslaChargingMap.ZoomLevel = 14;
        }

        private void UpdateAmapRouteAction()
        {
            bool selected = DestinationComboBox.SelectedItem is MapIcon { Location: not null };
            AmapRoutePanel.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            AmapRouteButton.IsEnabled = selected && vehicleMapLocation != null && !openingAmapRoute;
            var resources = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            AmapRouteHint.Text = resources.GetString(vehicleMapLocation == null ? "MapAmapNeedsVehicle" : "MapAmapRouteHint");
        }

        private async void AmapRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (openingAmapRoute || vehicleMapLocation == null
                || DestinationComboBox.SelectedItem is not MapIcon { Location: not null } site) return;
            openingAmapRoute = true;
            UpdateAmapRouteAction();
            try
            {
                var resources = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
                var start = vehicleMapLocation.Position;
                var destination = site.Location.Position;
                var uri = AmapRouteLink.Create(start.Latitude, start.Longitude,
                    destination.Latitude, destination.Longitude, resources.GetString("MapYourVehicle"), site.Title);
                if (!await Windows.System.Launcher.LaunchUriAsync(uri))
                    MainPage.Instance?.ShowToast(resources.GetString("MapAmapOpenFailed"));
            }
            catch
            {
                MainPage.Instance?.ShowToast(new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader().GetString("MapAmapOpenFailed"));
            }
            finally { openingAmapRoute = false; UpdateAmapRouteAction(); }
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
                    var parts = param.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && parts[0] == "code")
                    {
                        token = Uri.UnescapeDataString(parts[1]);
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
                var content = await TeslaFleetServices.ExchangeCodeAsync(token);
                return TeslaFleetServices.SaveSession(content);
            }
            catch
            {
                return false;
            }
        }

        private bool loadingVehicleMenu;
        private async void CarDropDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (loadingVehicleMenu) return;
            loadingVehicleMenu = true;
            try
            {
                string previousVin = AppSettings.Instance.Current_carvin;
                var cars = await ViewModel.GetCarListAsync(true);
                CarMenuFlyout.Items.Clear();
                if (cars == null)
                {
                    CarMenuFlyout.Items.Add(new MenuFlyoutItem { Text = "Unable to load vehicles", IsEnabled = false });
                    return;
                }
                foreach (var car in cars)
                {
                    string suffix = car.vin.Substring(Math.Max(0, car.vin.Length - 6));
                    var item = new Muxc.RadioMenuFlyoutItem
                    {
                        Tag = car.vin,
                        Text = (string.IsNullOrWhiteSpace(car.display_name) ? "Vehicle" : car.display_name) + " · " + suffix,
                        GroupName = "CarList",
                        IsChecked = AppSettings.Instance.Current_carvin == car.vin
                    };
                    item.Click += SwitchMainCarMfi_Click;
                    CarMenuFlyout.Items.Add(item);
                }
                if (cars.Count == 0)
                    CarMenuFlyout.Items.Add(new MenuFlyoutItem { Text = "No vehicles available", IsEnabled = false });
                if (previousVin != AppSettings.Instance.Current_carvin)
                {
                    ClearVehicleMap();
                    await ViewModel.Intitalize(AppSettings.Instance.Current_carvin);
                }
            }
            finally { loadingVehicleMenu = false; }
        }

        private void ClearVehicleMap()
        {
            mapLoadRevision++;
            TeslaChargingMap.MapElements.Clear();
            loadedMapVin = null;
            vehicleMapLocation = null;
            CenterVehicleButton.IsEnabled = false;
            DestinationComboBox.SelectedIndex = -1;
            DestinationComboBox.Visibility = Visibility.Collapsed;
        }

        private async void SwitchMainCarMfi_Click(object sender, RoutedEventArgs e)
        {
            string vin = (sender as Muxc.RadioMenuFlyoutItem)?.Tag as string;
            if (string.IsNullOrWhiteSpace(vin) || vin == AppSettings.Instance.Current_carvin) return;
            ClearVehicleMap();
            await ViewModel.Intitalize(vin);
        }

        private void RemoveUserSettings()
        {
            try
            {
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values.Remove("maincarvin");
                AppSettings.Instance.Current_carvin = null;
                ViewModel.CarList = null;
                ViewModel.CarData = null;
                CarMenuFlyout.Items.Clear();
                ClearVehicleMap();
                localSettings.Values.Remove("refreshtoken");
                localSettings.Values.Remove("accessTokenExpiresUtc");
                AppSettings.Instance.Access_token = null;
                AppSettings.Instance.Refresh_token = null;
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
            ContentDialogResult result = await TeslaMurphy.Helpers.DesktopHelper.ShowDialogAsync(dialog);
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

        private void VehicleAppearance_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(Model3HelixPage));
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
        //        string authRequestUrl = await TeslaFleetServices.GenerateAuthorizeUriAsync();
        //        UWPGeneralHelper.OpenInDefaultBrowser(authRequestUrl);
        //    }
        //    else
        //    {
        //        HandleCallback(new Uri("teslauwp://localhost:8000/?code=" + AuthorizeTextBlock.Text));
        //    }
        //}
    }
}
