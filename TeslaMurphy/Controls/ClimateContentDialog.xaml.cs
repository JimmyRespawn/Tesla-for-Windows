using System;
using System.Threading.Tasks;
using TeslaMurphy.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TeslaMurphy.Controls
{
    public sealed partial class ClimateContentDialog : ContentDialog
    {
        public ClimateState climateStateData { get; set; }
        public Func<bool, Task<bool>> ChangeClimateAsync { get; set; }
        public Func<string> GetCommandError { get; set; }
        private bool initializing = true;
        private bool busy;

        public ClimateContentDialog()
        {
            InitializeComponent();
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            initializing = true;
            if (climateStateData != null)
            {
                InteriorTemperatureTextBlock.Text = climateStateData.inside_temp + "°";
                ExteriorTemperatureTextBlock.Text = climateStateData.outside_temp + "°";
                ACModeSwitch.IsOn = climateStateData.is_climate_on;
                AutoACModeSwitch.IsOn = climateStateData.is_auto_conditioning_on;
            }
            ACModeSwitch.IsEnabled = climateStateData != null && ChangeClimateAsync != null && !AppSettings.Instance.IsTestMode;
            if (AppSettings.Instance.IsTestMode) StatusTextBlock.Text = "Vehicle controls are unavailable in demo mode.";
            else if (!ACModeSwitch.IsEnabled) StatusTextBlock.Text = "Climate data is unavailable.";
            initializing = false;
        }

        private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            args.Cancel = busy;
        }

        private async void ACModeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (initializing || busy || climateStateData == null || ChangeClimateAsync == null) return;
            bool previous = climateStateData.is_climate_on;
            bool requested = ACModeSwitch.IsOn;
            if (requested == previous) return;
            busy = true;
            ACModeSwitch.IsEnabled = false;
            IsSecondaryButtonEnabled = false;
            CommandProgress.IsActive = true;
            StatusTextBlock.Text = requested ? "Turning climate on…" : "Turning climate off…";
            try
            {
                if (AppSettings.Instance.IsTestMode) return;
                if (await ChangeClimateAsync(requested))
                {
                    climateStateData.is_climate_on = requested;
                    StatusTextBlock.Text = requested ? "Climate is on." : "Climate is off.";
                }
                else
                    StatusTextBlock.Text = GetCommandError?.Invoke() ?? "Climate command failed. Please try again.";
            }
            catch
            {
                StatusTextBlock.Text = "Unable to change climate. Check your connection and command proxy.";
            }
            finally
            {
                initializing = true;
                ACModeSwitch.IsOn = climateStateData.is_climate_on;
                initializing = false;
                busy = false;
                CommandProgress.IsActive = false;
                IsSecondaryButtonEnabled = true;
                ACModeSwitch.IsEnabled = !AppSettings.Instance.IsTestMode;
            }
        }
    }
}
