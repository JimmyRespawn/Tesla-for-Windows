using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class ChargeStateVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string chargeState = value.ToString();
            if (chargeState != "Charging")
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
