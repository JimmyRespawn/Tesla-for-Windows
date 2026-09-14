using System;

namespace TeslaMurphy.Helpers
{
    internal static class ChinaCoordinates
    {
        // Approximate GCJ-02 conversion. Bounds are a coarse guard, not a national border.
        public static double[] Convert(double latitude, double longitude, string mode)
        {
            if (mode == "None" || longitude < 72.004 || longitude > 137.8347 || latitude < 0.8293 || latitude > 55.8271)
                return new[] { latitude, longitude };
            if (mode == "Wgs84ToGcj02") return Forward(latitude, longitude);
            if (mode != "Gcj02ToWgs84") throw new ArgumentException("Unknown vehicle coordinate conversion.");
            double lat = latitude, lon = longitude;
            for (int i = 0; i < 8; i++)
            {
                var shifted = Forward(lat, lon);
                double dy = shifted[0] - latitude, dx = shifted[1] - longitude;
                lat -= dy; lon -= dx;
                if (Math.Abs(dy) < 1e-7 && Math.Abs(dx) < 1e-7) break;
            }
            return new[] { lat, lon };
        }

        private static double[] Forward(double lat, double lon)
        {
            double x = lon - 105, y = lat - 35, pi = Math.PI;
            double dy = -100 + 2*x + 3*y + 0.2*y*y + 0.1*x*y + 0.2*Math.Sqrt(Math.Abs(x));
            double dx = 300 + x + 2*y + 0.1*x*x + 0.1*x*y + 0.1*Math.Sqrt(Math.Abs(x));
            double common = (20*Math.Sin(6*x*pi) + 20*Math.Sin(2*x*pi))*2/3;
            dy += common + (20*Math.Sin(y*pi) + 40*Math.Sin(y*pi/3))*2/3
                + (160*Math.Sin(y*pi/12) + 320*Math.Sin(y*pi/30))*2/3;
            dx += common + (20*Math.Sin(x*pi) + 40*Math.Sin(x*pi/3))*2/3
                + (150*Math.Sin(x*pi/12) + 300*Math.Sin(x*pi/30))*2/3;
            double rad = lat*pi/180, sin = Math.Sin(rad);
            const double a = 6378245, ee = 0.00669342162296594323;
            double magic = 1-ee*sin*sin, root = Math.Sqrt(magic);
            dy = dy*180/((a*(1-ee)/(magic*root))*pi);
            dx = dx*180/((a/root)*Math.Cos(rad)*pi);
            return new[] { lat+dy, lon+dx };
        }
    }
}
