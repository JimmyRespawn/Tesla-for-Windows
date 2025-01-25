using System;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class LockStateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string isLocked = value.ToString();
            if (isLocked == "True")
                return "\uE72E";
            return "\uE785";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
