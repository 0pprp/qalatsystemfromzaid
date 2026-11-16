using System.Globalization;

namespace BE_Company.Sales.Services;

public enum ShopLocationConfidence : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
}

public readonly record struct GeoPoint(double Latitude, double Longitude);

public readonly record struct CustomerPaymentPoint(int CustomerId, double Latitude, double Longitude);

public sealed record ShopLocationEstimate(
    int CustomerId,
    double Latitude,
    double Longitude,
    ShopLocationConfidence Confidence,
    int ClusterPoints,
    int TotalPoints);

/// <summary>
/// Estimates a customer's shop location from the GPS points saved with their payments
/// (the delegate's phone position at the time of payment).
/// 1) Points where the delegate recorded payments for many different customers of the same
///    list (office, home, café) are dropped — they are nobody's shop.
/// 2) The remaining points per customer are grouped (50 m); the biggest group is the shop.
/// 3) The pin is the median of that group; confidence comes from group size and share.
/// </summary>
public static class ShopLocationEstimator
{
    public const double ClusterRadiusMeters = 50;
    public const double OfficeRadiusMeters = 50;

    /// <summary>A point with payments from this many OTHER customers within 50 m is an office/home spot.</summary>
    public const int OfficeMinOtherCustomers = 8;

    public const int HighMinPoints = 5;
    public const int MediumMinPoints = 2;
    public const double ScatteredShare = 0.3;

    // Iraq bounding box — also drops the 0000,0000 / 1111,1111 placeholders the apps store.
    private const double MinLat = 29, MaxLat = 38, MinLng = 38, MaxLng = 49;

    /// <summary>Parses "lat,lng" (or swapped "lng,lat"); null for placeholders or anything outside Iraq.</summary>
    public static GeoPoint? ParseLocation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var comma = raw.IndexOf(',');
        if (comma <= 0 || comma >= raw.Length - 1)
        {
            return null;
        }

        if (!double.TryParse(raw[..comma].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
            || !double.TryParse(raw[(comma + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
        {
            return null;
        }

        return Normalize(a, b);
    }

    public static GeoPoint? Normalize(double lat, double lng)
    {
        if (IsInside(lat, lng))
        {
            return new GeoPoint(lat, lng);
        }

        if (IsInside(lng, lat))
        {
            return new GeoPoint(lng, lat);
        }

        return null;
    }

    public static bool IsInside(double lat, double lng) =>
        lat is >= MinLat and <= MaxLat && lng is >= MinLng and <= MaxLng;

    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        // Equirectangular approximation — accurate to well under 1 m at these distances.
        const double R = 6_371_000;
        var phi = (lat1 + lat2) * 0.5 * Math.PI / 180;
        var dx = (lng2 - lng1) * Math.PI / 180 * Math.Cos(phi);
        var dy = (lat2 - lat1) * Math.PI / 180;
        return Math.Sqrt(dx * dx + dy * dy) * R;
    }

    /// <summary>Points of one list (all its customers) → one estimate per customer that has usable points.</summary>
    public static IReadOnlyList<ShopLocationEstimate> Estimate(IReadOnlyList<CustomerPaymentPoint> points)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var kept = DropOfficePoints(points);
        return kept
            .GroupBy(p => p.CustomerId)
            .Select(g => EstimateCustomer(g.Key, g.ToList()))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
    }

    public static List<CustomerPaymentPoint> DropOfficePoints(IReadOnlyList<CustomerPaymentPoint> points)
    {
        var flags = OfficeFlags(points, points);
        var kept = new List<CustomerPaymentPoint>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            if (!flags[i])
            {
                kept.Add(points[i]);
            }
        }

        return kept;
    }

    /// <summary>For each target point: true when payments of 8+ other customers of the list were recorded within 50 m.</summary>
    public static bool[] OfficeFlags(IReadOnlyList<CustomerPaymentPoint> listPoints, IReadOnlyList<CustomerPaymentPoint> targets)
    {
        var grid = new SpatialGrid(OfficeRadiusMeters, listPoints);
        var flags = new bool[targets.Count];
        var others = new HashSet<int>();
        for (var i = 0; i < targets.Count; i++)
        {
            var p = targets[i];
            others.Clear();
            foreach (var q in grid.Near(p.Latitude, p.Longitude))
            {
                if (q.CustomerId == p.CustomerId || others.Contains(q.CustomerId))
                {
                    continue;
                }

                if (DistanceMeters(p.Latitude, p.Longitude, q.Latitude, q.Longitude) > OfficeRadiusMeters)
                {
                    continue;
                }

                others.Add(q.CustomerId);
                if (others.Count >= OfficeMinOtherCustomers)
                {
                    flags[i] = true;
                    break;
                }
            }
        }

        return flags;
    }

    public static ShopLocationEstimate? EstimateCustomer(int customerId, IReadOnlyList<CustomerPaymentPoint> pts)
    {
        var total = pts.Count;
        if (total == 0)
        {
            return null;
        }

        // Densest point is the seed of the main group.
        var seed = 0;
        var seedCount = -1;
        for (var i = 0; i < total; i++)
        {
            var c = 0;
            for (var j = 0; j < total; j++)
            {
                if (DistanceMeters(pts[i].Latitude, pts[i].Longitude, pts[j].Latitude, pts[j].Longitude) <= ClusterRadiusMeters)
                {
                    c++;
                }
            }

            if (c > seedCount)
            {
                seedCount = c;
                seed = i;
            }
        }

        var members = Within(pts, pts[seed].Latitude, pts[seed].Longitude);
        var center = Median(members);
        // One refinement around the median so the group isn't biased by the seed's position.
        var refined = Within(pts, center.Latitude, center.Longitude);
        if (refined.Count >= members.Count)
        {
            members = refined;
            center = Median(members);
        }

        var n = members.Count;
        var share = (double)n / total;
        var confidence =
            n >= HighMinPoints && share > 0.5 ? ShopLocationConfidence.High
            : n >= MediumMinPoints && share >= ScatteredShare ? ShopLocationConfidence.Medium
            : ShopLocationConfidence.Low;

        return new ShopLocationEstimate(customerId, center.Latitude, center.Longitude, confidence, n, total);
    }

    private static List<CustomerPaymentPoint> Within(IReadOnlyList<CustomerPaymentPoint> pts, double lat, double lng) =>
        pts.Where(p => DistanceMeters(lat, lng, p.Latitude, p.Longitude) <= ClusterRadiusMeters).ToList();

    private static GeoPoint Median(IReadOnlyList<CustomerPaymentPoint> pts) =>
        new(MedianOf(pts.Select(p => p.Latitude)), MedianOf(pts.Select(p => p.Longitude)));

    private static double MedianOf(IEnumerable<double> values)
    {
        var s = values.OrderBy(v => v).ToArray();
        var mid = s.Length / 2;
        return s.Length % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
    }

    /// <summary>Uniform grid (cell = radius) so neighbour lookups only scan the 3×3 surrounding cells.</summary>
    private sealed class SpatialGrid
    {
        private readonly double _cellLat;
        private readonly double _cellLng;
        private readonly Dictionary<(int, int), List<CustomerPaymentPoint>> _cells = new();

        public SpatialGrid(double radiusMeters, IReadOnlyList<CustomerPaymentPoint> points)
        {
            _cellLat = radiusMeters / 111_320.0;
            // Iraq spans ~29–38°N; using the northern edge keeps cells at least `radius` wide everywhere.
            _cellLng = radiusMeters / (111_320.0 * Math.Cos(MaxLat * Math.PI / 180));
            foreach (var p in points)
            {
                var key = Key(p.Latitude, p.Longitude);
                if (!_cells.TryGetValue(key, out var list))
                {
                    _cells[key] = list = [];
                }

                list.Add(p);
            }
        }

        private (int, int) Key(double lat, double lng) =>
            ((int)Math.Floor(lat / _cellLat), (int)Math.Floor(lng / _cellLng));

        public IEnumerable<CustomerPaymentPoint> Near(double lat, double lng)
        {
            var (ky, kx) = Key(lat, lng);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (_cells.TryGetValue((ky + dy, kx + dx), out var list))
                    {
                        foreach (var q in list)
                        {
                            yield return q;
                        }
                    }
                }
            }
        }
    }
}
