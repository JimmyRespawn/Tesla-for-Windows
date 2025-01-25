using System;
using Windows.UI.Xaml.Data;

namespace TeslaMurphy.Converters
{
    public class UpdateTimestampToDesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                var unixTimeMilliseconds = (long)value;
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
                DateTime localDateTime = dateTimeOffset.LocalDateTime;
                var timeSpan = DateTime.Now.Subtract(localDateTime);
                if (timeSpan > TimeSpan.FromHours(24))
                    return localDateTime.ToString("MM/dd/yyyy hh:mm:ss tt");
                else if (timeSpan <= TimeSpan.FromSeconds(60))
                    return string.Format("{0} seconds ago", timeSpan.Seconds);
                else if (timeSpan <= TimeSpan.FromMinutes(60))
                    return timeSpan.Minutes > 1 ?
                        String.Format("{0} minutes ago", timeSpan.Minutes) :
                        "1 minute ago";
                else if (timeSpan <= TimeSpan.FromHours(24))
                    return timeSpan.Hours > 1 ?
                        String.Format("{0} hours ago", timeSpan.Hours) :
                        "1 hour ago";
                return localDateTime.ToString("MM/dd/yyyy hh:mm:ss tt");
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
