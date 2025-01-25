using System;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class BooleanToOpenCloseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string isOpened = value.ToString();
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            if (isOpened == "True")
                return resourceLoader.GetString("Unlocked/Text");
            return resourceLoader.GetString("Locked/Text");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
