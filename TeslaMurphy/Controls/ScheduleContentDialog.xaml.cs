using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TeslaMurphy.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TeslaMurphy.Controls
{
    public sealed partial class ScheduleContentDialog : ContentDialog
    {
        public ChargeStateData chargeStateData { get; set; }
        public Func<bool, string, Task<bool>> SaveScheduleAsync { get; set; }
        public Func<string> GetCommandError { get; set; }
        private bool busy;
        public ScheduleContentDialog() { InitializeComponent(); }
        private static TimeSpan Time(int minutes) => TimeSpan.FromMinutes(Math.Max(0, Math.Min(1439, minutes)));
        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            var data = chargeStateData;
            bool available = data != null && SaveScheduleAsync != null && !AppSettings.Instance.IsTestMode;
            //Editor.IsEnabled = IsPrimaryButtonEnabled = available;
            if (data == null) { Status.Text = "Schedule data is unavailable."; return; }
            Mode.SelectedIndex = data.scheduled_charging_mode == "StartAt" ? 1 : 0;
            Enabled.IsOn = data.scheduled_charging_mode == "StartAt" || data.scheduled_charging_mode == "DepartBy";
            Departure.Time = Time(data.scheduled_departure_time_minutes);
            ChargeStart.Time = Time(data.scheduled_charging_start_time_minutes);
            OffPeakEnd.Time = Time(data.off_peak_hours_end_time);
            Precondition.IsOn = data.preconditioning_enabled;
            OffPeak.IsOn = data.off_peak_charging_enabled;
            PreconditionWeekdays.IsChecked = data.preconditioning_weekdays_only;
            OffPeakWeekdays.IsChecked = data.off_peak_charging_weekdays_only;
            if (AppSettings.Instance.IsTestMode) Status.Text = "Scheduling is unavailable in demo mode.";
        }
        private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args) { args.Cancel = busy; }
        private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (busy || SaveScheduleAsync == null || chargeStateData == null || AppSettings.Instance.IsTestMode) return;
            bool departure = Mode.SelectedIndex == 0;
            bool enabled = Enabled.IsOn;
            int departureTime = (int)Departure.Time.TotalMinutes;
            int startTime = (int)ChargeStart.Time.TotalMinutes;
            int endTime = (int)OffPeakEnd.Time.TotalMinutes;
            if (departureTime < 0 || departureTime > 1439 || startTime < 0 || startTime > 1439 || endTime < 0 || endTime > 1439)
            { Status.Text = "Please select a valid time."; return; }
            if (departure && enabled && !Precondition.IsOn && !OffPeak.IsOn)
            { Status.Text = "Enable preconditioning or off-peak charging for departure."; return; }
            object payload = departure ? (object)new {
                enable = enabled, departure_time = departureTime,
                preconditioning_enabled = Precondition.IsOn,
                preconditioning_weekdays_only = PreconditionWeekdays.IsChecked == true,
                off_peak_charging_enabled = OffPeak.IsOn,
                off_peak_charging_weekdays_only = OffPeakWeekdays.IsChecked == true,
                end_off_peak_time = endTime
            } : new { enable = enabled, time = startTime };
            busy = true;
            //Editor.IsEnabled = IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = false;
            Progress.IsActive = true;
            Status.Text = "Saving schedule…";
            try
            {
                if (!await SaveScheduleAsync(departure, JsonConvert.SerializeObject(payload)))
                { Status.Text = GetCommandError?.Invoke() ?? "Unable to save the schedule."; return; }
                var data = chargeStateData;
                string selectedMode = departure ? "DepartBy" : "StartAt";
                if (enabled || data.scheduled_charging_mode == selectedMode)
                    data.scheduled_charging_mode = enabled ? selectedMode : "Off";
                if (departure)
                {
                    data.scheduled_departure_time_minutes = departureTime;
                    data.preconditioning_enabled = enabled && Precondition.IsOn;
                    data.off_peak_charging_enabled = enabled && OffPeak.IsOn;
                    data.preconditioning_weekdays_only = PreconditionWeekdays.IsChecked == true;
                    data.off_peak_charging_weekdays_only = OffPeakWeekdays.IsChecked == true;
                    data.off_peak_hours_end_time = endTime;
                }
                else data.scheduled_charging_start_time_minutes = startTime;
                Status.Text = "Schedule saved to the vehicle.";
            }
            catch { Status.Text = "Schedule not saved. Check the vehicle connection and command proxy."; }
            finally
            {
                busy = false;
                Progress.IsActive = false;
                //Editor.IsEnabled = IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = true;
            }
        }
    }
}
