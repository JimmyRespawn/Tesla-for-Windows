using System;
using Microsoft.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class BooleanToOpenCloseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((string)parameter == "Door") return value is bool open ? (open ? "Open" : "Closed") : "Unavailable";
            string isOpened = value?.ToString();
            var resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
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
