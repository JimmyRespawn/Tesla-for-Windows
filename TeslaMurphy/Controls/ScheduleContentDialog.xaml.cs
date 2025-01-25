using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TeslaMurphy.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TeslaMurphy.Controls
{
    public sealed partial class ScheduleContentDialog : ContentDialog
    {
        public ChargeStateData chargeStateData { get; set; }
        public ScheduleContentDialog()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var themeMode = AppSettings.Instance.CurrentTheme.ToString();
                if (themeMode == "Dark")
                    this.RequestedTheme = ElementTheme.Dark;
                else
                    this.RequestedTheme = ElementTheme.Light;
                if (chargeStateData != null)
                {
                    StartChargingTextBlock.Text = ConvertMinutesToTimeString(chargeStateData.scheduled_charging_start_time_minutes);
                    DepartureTimeTextBlock.Text = ConvertMinutesToTimeString(chargeStateData.scheduled_departure_time_minutes);
                    PreconditionToggleSwitch.IsOn = chargeStateData.preconditioning_enabled;
                    OffPeakChargeToggleSwitch.IsOn = chargeStateData.off_peak_charging_enabled;
                    SheduledChargeToggleSwitch.IsOn = chargeStateData.scheduled_charging_pending;
                }
            }
            catch
            {

            }
        }
        public string ConvertMinutesToTimeString(int totalMinutes)
        {
            try
            {
                // 计算小时数
                int hours = totalMinutes / 60;

                // 计算剩余的分钟数
                int minutes = totalMinutes % 60;

                // 返回 HH:mm 格式的时间字符串
                return $"{hours:D2}:{minutes:D2}";
            }
            catch
            {
                return "";
            }
        }
    }
}
