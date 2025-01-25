using System;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class CarModelToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string modelString = value.ToString();
            if (modelString == "model3")
                return "/Assets/Images/model3pearlwhite.webp";
            else if (modelString == "modely")
                return "/Assets/Images/modelypearlwhite.webp";
            else if (modelString == "models")
                return "/Assets/Images/modelspearlwhite.webp";
            else if (modelString == "modelx")
                return "/Assets/Images/modelxpearlwhite.webp";
            return "/Assets/Images/modelypearlwhite.webp";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
