using System.Globalization;

namespace BE_Company.Sales.Services
{
    public static class SalesShopMaps
    {
        public static string? GoogleMapsUrl(double? latitude, double? longitude)
        {
            if (latitude is null || longitude is null)
            {
                return null;
            }

            if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            {
                return null;
            }

            var lat = latitude.Value.ToString("0.######", CultureInfo.InvariantCulture);
            var lng = longitude.Value.ToString("0.######", CultureInfo.InvariantCulture);
            return $"https://www.google.com/maps?q={lat},{lng}";
        }
    }
}
