using System;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class BatteryStateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var charge = value as TeslaMurphy.Models.ChargeStateData;
            int percentage;
            if (charge != null)
                percentage = charge.battery_level;
            else if (!int.TryParse(value?.ToString(), out percentage))
                return "\uEBA0";

            int level = Math.Max(0, Math.Min(100, percentage)) / 10;
            bool charging = string.Equals(charge?.charging_state, "Charging", StringComparison.Ordinal);
            return char.ConvertFromUtf32((charging ? 0xEBAB : 0xEBA0) + level);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
