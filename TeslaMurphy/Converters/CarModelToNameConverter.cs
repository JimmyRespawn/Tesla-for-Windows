using System;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class CarModelToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string carModel = value.ToString();
            if (carModel == "model3")
                return "Model 3";
            else if (carModel == "modely")
                return "Model Y";
            else if (carModel == "modelx")
                return "Model X";
            else if (carModel == "models")
                return "Model S";
            return carModel;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
