using System;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TeslaMurphy.Models;
using TeslaMurphy.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TeslaMurphy.Controls
{
    public sealed partial class VehicleHealthContentDialog : ContentDialog
    {
        public string VehicleVin { get; set; }
        public string VehicleName { get; set; }
        public JObject VehicleSnapshot { get; set; }
        private VehicleSoftwareService service;
        private bool busy;
        private string updateStatus;
        private double? fullRangeKm;
        private bool baselineLoaded;
        private bool automaticBaseline;
        private bool settingBaseline;
        private JArray presetRows;
        private JArray chinaRows;
        private RangePresetService.Match activePreset;
        private string BaselineKey => "OriginalRatedRangeKm:" + VehicleVin.ToUpperInvariant();
        public VehicleHealthContentDialog() { InitializeComponent(); }
        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            VehicleLabel.Text = (VehicleName ?? "Vehicle") + " · " + VehicleVin;
            if (!string.IsNullOrWhiteSpace(VehicleVin))
            {
                var saved = ApplicationData.Current.LocalSettings.Values[BaselineKey];
                if (saved is double) OriginalRange.Text = ((double)saved).ToString(CultureInfo.CurrentCulture);
                baselineLoaded = true;
            }
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/tesla-epa-ranges.json"));
                presetRows = (JArray)JObject.Parse(await FileIO.ReadTextAsync(file))["vehicles"];
            }
            catch { PresetStatus.Text = "Range reference catalog could not be loaded."; }
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/tesla-cn-ranges.json"));
                chinaRows = (JArray)JObject.Parse(await FileIO.ReadTextAsync(file))["vehicles"];
            }
            catch { PresetStatus.Text = "China reference catalog could not be loaded."; }
            if (string.Equals((string)VehicleSnapshot?["vin"], VehicleVin, StringComparison.OrdinalIgnoreCase))
            {
                ApplyPreset(VehicleSnapshot);
                UpdateRange(VehicleSnapshot?["charge_state"] as JObject);
                Installed.Text = (string)VehicleSnapshot?["vehicle_state"]?["car_version"] ?? "—";
                if (fullRangeKm.HasValue) RangeStatus.Text = "Using the vehicle data currently shown on CarPage; refreshing…";
            }
            if (AppSettings.Instance.IsTestMode)
            {
                RefreshButton.IsEnabled = false;
                RangeStatus.Text = fullRangeKm.HasValue ? "Sample vehicle data (demo mode)." : "Sample range data is unavailable.";
                Status.Text = "Software updates are unavailable in demo mode.";
                return;
            }
            await Run(async () => { service = new VehicleSoftwareService(VehicleVin); await Refresh(); });
        }
        private void Range_Changed(object sender, TextChangedEventArgs e)
        {
            if (HealthText == null || HealthLabel == null || HealthHint == null || OriginalRange == null) return;
            double baseline;
            bool valid = double.TryParse(OriginalRange.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out baseline)
                && baseline > 0 && baseline <= 2000;
            bool userEdit = sender != null && baselineLoaded && !settingBaseline;
            if (userEdit)
            {
                automaticBaseline = false;
                PresetStatus.Text = valid ? "Your saved original range for this vehicle." : "Enter a valid original range, or reopen to use an available preset.";
                PresetSource.Visibility = Visibility.Collapsed;
            }
            if (userEdit && !AppSettings.Instance.IsTestMode)
            {
                if (valid) ApplicationData.Current.LocalSettings.Values[BaselineKey] = baseline;
                else ApplicationData.Current.LocalSettings.Values.Remove(BaselineKey);
            }
            if (automaticBaseline && activePreset?.MinimumKm != null && activePreset.MaximumKm > activePreset.MinimumKm)
            {
                HealthLabel.Text = "Vs. CN reference";
                HealthText.FontSize = 22;
                HealthText.Text = fullRangeKm.HasValue
                    ? (fullRangeKm.Value / activePreset.MaximumKm.Value * 100).ToString("F1") + "–" + (fullRangeKm.Value / activePreset.MinimumKm.Value * 100).ToString("F1") + "%" : "—";
                HealthHint.Text = "Owner-reported interval";
                return;
            }
            HealthText.FontSize = 32;
            HealthLabel.Text = automaticBaseline ? (activePreset?.Basis == "CN community" ? "Vs. CN reference" : "Vs. EPA reference") : "Range retention";
            HealthText.Text = valid && fullRangeKm.HasValue ? (fullRangeKm.Value / baseline * 100).ToString("F1") + "%" : "—";
            HealthHint.Text = !valid ? "Add a valid baseline below" : !fullRangeKm.HasValue ? "Waiting for vehicle data" : automaticBaseline ? "Reference comparison" : "Of original displayed range";
        }
        private void Details_Click(object sender, RoutedEventArgs e)
        {
            bool expanded = CalculationDetails.Visibility != Visibility.Visible;
            CalculationDetails.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            DetailsButton.Content = expanded ? "Hide details" : "Calculation details";
        }

        private void ApplyPreset(JObject vehicle)
        {
            if ((presetRows == null && chinaRows == null) || !string.Equals((string)vehicle?["vin"], VehicleVin, StringComparison.OrdinalIgnoreCase)) return;
            if (!automaticBaseline && !string.IsNullOrWhiteSpace(OriginalRange.Text))
            {
                PresetStatus.Text = "Your saved original range for this vehicle.";
                return;
            }
            var match = RangePresetService.Find(vehicle, presetRows ?? new JArray(), chinaRows);
            activePreset = match;
            settingBaseline = true;
            try
            {
                automaticBaseline = match.Kilometers.HasValue || match.MinimumKm.HasValue;
                OriginalRange.Text = match.Kilometers.HasValue ? match.Kilometers.Value.ToString("F1", CultureInfo.CurrentCulture)
                    : match.MinimumKm.HasValue ? match.MinimumKm.Value.ToString("F0") + "–" + match.MaximumKm.Value.ToString("F0") : "";
                PresetStatus.Text = match.Description;
                PresetSource.Visibility = match.SourceUrl == null ? Visibility.Collapsed : Visibility.Visible;
                PresetSource.NavigateUri = match.SourceUrl == null ? null : new Uri(match.SourceUrl);
            }
            finally { settingBaseline = false; }
            Range_Changed(null, null);
        }
        private void UpdateRange(JObject charge)
        {
            fullRangeKm = VehicleSoftwareService.EstimateFullRangeKm(charge);
            CurrentRange.Text = fullRangeKm.HasValue ? fullRangeKm.Value.ToString("F1") : "—";
            RangeStatus.Text = fullRangeKm.HasValue ? "Calculated from the latest vehicle response."
                : "A positive battery_range and battery_level (1–100%) are required. The vehicle did not provide valid values.";
            Range_Changed(null, null);
        }
        private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args) { args.Cancel = busy; }
        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            RefreshButton.IsEnabled = InstallButton.IsEnabled = CancelButton.IsEnabled = ConfirmButton.IsEnabled = false;
            IsSecondaryButtonEnabled = false;
            BusyProgress.Visibility = Visibility.Visible;
            Status.Text = "";
            try { await action(); }
            catch (Exception ex)
            {
                if (fullRangeKm.HasValue) RangeStatus.Text = "Showing previously loaded vehicle data; the latest request failed.";
                else RangeStatus.Text = "Vehicle range could not be loaded. See the request error below.";
                updateStatus = null;
                UpdateState.Text = "Status unavailable. Check again before sending a command.";
                UpdateProgress.Visibility = Visibility.Collapsed;
                Status.Text = ex is InvalidOperationException ? ex.Message : "Unable to read vehicle software data. Please check again.";
            }
            finally
            {
                busy = false;
                RefreshButton.IsEnabled = service != null && !AppSettings.Instance.IsTestMode;
                ConfirmButton.IsEnabled = IsSecondaryButtonEnabled = true;
                InstallButton.IsEnabled = updateStatus == "available";
                CancelButton.IsEnabled = updateStatus == "scheduled";
                BusyProgress.Visibility = Visibility.Collapsed;
            }
        }
        private async Task Refresh()
        {
            updateStatus = null;
            Confirmation.Visibility = Visibility.Collapsed;
            Available.Text = "—";
            UpdateProgress.Visibility = Visibility.Collapsed;
            var data = await service.RefreshAsync(message => Status.Text = message);
            ApplyPreset(data);
            var charge = data["charge_state"] as JObject;
            if (VehicleSoftwareService.EstimateFullRangeKm(charge).HasValue || !fullRangeKm.HasValue)
                UpdateRange(charge);
            else
                RangeStatus.Text = "Showing previously loaded vehicle data; the latest response has no valid battery range or charge level.";
            var state = data["vehicle_state"] as JObject ?? new JObject();
            Installed.Text = (string)state["car_version"] ?? "—";
            var update = state["software_update"] as JObject;
            updateStatus = ((string)update?["status"] ?? "").ToLowerInvariant();
            Available.Text = string.IsNullOrWhiteSpace((string)update?["version"]) ? "Not reported" : (string)update["version"];
            UpdateState.Text = string.IsNullOrEmpty(updateStatus) ? "No update reported by the vehicle." : "Vehicle status: " + updateStatus;
            var percent = update?[updateStatus == "installing" ? "install_perc" : "download_perc"];
            if ((updateStatus == "downloading" || updateStatus == "installing") && percent != null && percent.Type != JTokenType.Null)
            {
                UpdateProgress.Value = Math.Max(0, Math.Min(100, (double)percent));
                UpdateProgress.Visibility = Visibility.Visible;
                UpdateState.Text += " · " + UpdateProgress.Value.ToString("F0") + "%";
            }
            Status.Text = "Checked at " + DateTime.Now.ToString("T");
        }
        private async void Refresh_Click(object sender, RoutedEventArgs e) { await Run(Refresh); }
        private void Install_Click(object sender, RoutedEventArgs e) { if (!busy && updateStatus == "available") Confirmation.Visibility = Visibility.Visible; }
        private void Back_Click(object sender, RoutedEventArgs e) { Confirmation.Visibility = Visibility.Collapsed; }
        private async void Confirm_Click(object sender, RoutedEventArgs e) { await Command(false); }
        private async void Cancel_Click(object sender, RoutedEventArgs e) { await Command(true); }
        private async Task Command(bool cancel)
        {
            await Run(async () =>
            {
                await Refresh();
                if (updateStatus != (cancel ? "scheduled" : "available"))
                {
                    Status.Text = "Vehicle status changed. The command was not sent.";
                    return;
                }
                updateStatus = null;
                await service.SendAsync(cancel);
                await Refresh();
                Status.Text = "Command accepted. Vehicle status may take time to update; check again shortly.";
            });
        }
    }
}
