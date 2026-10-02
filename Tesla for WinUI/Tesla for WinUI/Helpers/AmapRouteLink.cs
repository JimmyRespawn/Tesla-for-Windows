using System;
using System.Globalization;

namespace TeslaMurphy.Helpers;

internal static class AmapRouteLink
{
    // Inputs are WGS84 points from the homepage; Amap's route URI uses GCJ-02.
    public static Uri Create(double startLatitude, double startLongitude,
        double destinationLatitude, double destinationLongitude,
        string startName, string destinationName)
    {
        string start = Point(startLatitude, startLongitude, startName);
        string destination = Point(destinationLatitude, destinationLongitude, destinationName);
        return new Uri("https://uri.amap.com/navigation?from=" + Uri.EscapeDataString(start)
            + "&to=" + Uri.EscapeDataString(destination)
            + "&mode=car&policy=0&src=TeslaWinUI&coordinate=gaode&callnative=0");
    }

    private static string Point(double latitude, double longitude, string name)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude)
            || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        var position = ChinaCoordinates.Convert(latitude, longitude, "Wgs84ToGcj02");
        // Commas delimit the coordinate/name tuple, so names cannot contain them.
        string label = (name ?? "").Replace(',', '，').Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (label.Length > 100) label = label.Substring(0, 100);
        return position[1].ToString("0.########", CultureInfo.InvariantCulture) + ","
            + position[0].ToString("0.########", CultureInfo.InvariantCulture) + "," + label;
    }
}
