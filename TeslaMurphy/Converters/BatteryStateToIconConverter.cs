using System;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class BatteryStateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string batteryPercentage = value.ToString();
            int intBatteryPercentage = int.Parse(batteryPercentage);
            if (intBatteryPercentage == 100)
                return "\uEBAA";
            else if (intBatteryPercentage >= 90)
                return "\uEBA9";
            else if (intBatteryPercentage >= 80)
                return "\uEBA8";
            else if (intBatteryPercentage >= 70)
                return "\uEBA7";
            else if (intBatteryPercentage >= 60)
                return "\uEBA6";
            else if (intBatteryPercentage >= 50)
                return "\uEBA5";
            else if (intBatteryPercentage >= 40)
                return "\uEBA4";
            else if (intBatteryPercentage >= 30)
                return "\uEBA3";
            else if (intBatteryPercentage >= 20)
                return "\uEBA2";
            else if (intBatteryPercentage >= 10)
                return "\uEBA1";
            return "\uEBA0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
