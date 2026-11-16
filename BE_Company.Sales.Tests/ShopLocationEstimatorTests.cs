using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class ShopLocationEstimatorTests
{
    // Baghdad area. ~0.00009° latitude ≈ 10 m.
    private const double Lat = 33.3152, Lng = 44.3661;
    private const double TenMetersLat = 0.00009;

    private static CustomerPaymentPoint P(int customer, double dLatMeters, double dLngMeters = 0) =>
        new(customer, Lat + dLatMeters / 10 * TenMetersLat, Lng + dLngMeters / 10 * TenMetersLat / Math.Cos(Lat * Math.PI / 180));

    [Theory]
    [InlineData("33.3152,44.3661", 33.3152, 44.3661)]
    [InlineData(" 33.3152 , 44.3661 ", 33.3152, 44.3661)]
    [InlineData("44.3661,33.3152", 33.3152, 44.3661)] // swapped lng,lat
    public void ParseLocation_reads_lat_lng(string raw, double lat, double lng)
    {
        var p = ShopLocationEstimator.ParseLocation(raw);
        Assert.NotNull(p);
        Assert.Equal(lat, p!.Value.Latitude, 6);
        Assert.Equal(lng, p.Value.Longitude, 6);
    }

    [Theory]
    [InlineData("0000,0000")]
    [InlineData("1111,1111")]
    [InlineData("0,0")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc,def")]
    [InlineData("51.5,-0.12")] // outside Iraq
    public void ParseLocation_rejects_placeholders_and_outside_iraq(string? raw) =>
        Assert.Null(ShopLocationEstimator.ParseLocation(raw));

    [Fact]
    public void Tight_group_of_five_or_more_is_high_confidence_at_the_median()
    {
        var pts = new[] { P(1, 0), P(1, 5), P(1, -5), P(1, 10), P(1, -10), P(1, 2) };
        var e = ShopLocationEstimator.EstimateCustomer(1, pts);
        Assert.NotNull(e);
        Assert.Equal(ShopLocationConfidence.High, e!.Confidence);
        Assert.Equal(6, e.ClusterPoints);
        Assert.True(ShopLocationEstimator.DistanceMeters(Lat, Lng, e.Latitude, e.Longitude) < 5);
    }

    [Fact]
    public void Stray_point_does_not_move_the_pin()
    {
        var pts = new[] { P(1, 0), P(1, 5), P(1, -5), P(1, 3), P(1, -3), P(1, 900) };
        var e = ShopLocationEstimator.EstimateCustomer(1, pts)!;
        Assert.Equal(5, e.ClusterPoints);
        Assert.Equal(6, e.TotalPoints);
        Assert.True(ShopLocationEstimator.DistanceMeters(Lat, Lng, e.Latitude, e.Longitude) < 5);
    }

    [Fact]
    public void Two_to_four_points_in_the_group_is_medium()
    {
        var e = ShopLocationEstimator.EstimateCustomer(1, [P(1, 0), P(1, 8), P(1, -8)])!;
        Assert.Equal(ShopLocationConfidence.Medium, e.Confidence);
    }

    [Fact]
    public void Single_point_is_low()
    {
        var e = ShopLocationEstimator.EstimateCustomer(1, [P(1, 0)])!;
        Assert.Equal(ShopLocationConfidence.Low, e.Confidence);
    }

    [Fact]
    public void Scattered_points_are_low()
    {
        // 7 points ~300 m apart: biggest group is 1 of 7.
        var pts = Enumerable.Range(0, 7).Select(i => P(1, i * 300)).ToArray();
        var e = ShopLocationEstimator.EstimateCustomer(1, pts)!;
        Assert.Equal(ShopLocationConfidence.Low, e.Confidence);
    }

    [Fact]
    public void Five_in_group_but_not_the_majority_is_medium_not_high()
    {
        var pts = Enumerable.Range(0, 5).Select(i => P(1, i * 2))
            .Concat(Enumerable.Range(0, 6).Select(i => P(1, 2000 + i * 400)))
            .ToArray();
        var e = ShopLocationEstimator.EstimateCustomer(1, pts)!;
        Assert.Equal(5, e.ClusterPoints);
        Assert.Equal(ShopLocationConfidence.Medium, e.Confidence);
    }

    [Fact]
    public void Office_spot_used_for_many_customers_is_dropped()
    {
        var points = new List<CustomerPaymentPoint>();
        // 10 customers each paid twice at the office (within a few meters).
        for (var c = 1; c <= 10; c++)
        {
            points.Add(P(c, c % 3));
            points.Add(P(c, -(c % 3)));
        }

        // Customer 1 also has a real shop 2 km away with 3 points.
        points.Add(P(1, 2000));
        points.Add(P(1, 2005));
        points.Add(P(1, 1995));

        var estimates = ShopLocationEstimator.Estimate(points).ToDictionary(e => e.CustomerId);

        var shop = estimates[1];
        Assert.Equal(3, shop.TotalPoints);
        Assert.True(ShopLocationEstimator.DistanceMeters(Lat, Lng, shop.Latitude, shop.Longitude) > 1900);

        // Customers that only ever paid at the office get no pin at all.
        Assert.False(estimates.ContainsKey(2));
    }

    [Fact]
    public void OfficeFlags_marks_only_the_shared_spot_for_one_customer()
    {
        var points = new List<CustomerPaymentPoint>();
        for (var c = 1; c <= 10; c++)
        {
            points.Add(P(c, c % 3));
        }

        var shop = P(1, 2000);
        points.Add(shop);

        var mine = points.Where(p => p.CustomerId == 1).ToList();
        var flags = ShopLocationEstimator.OfficeFlags(points, mine);
        Assert.Equal([true, false], flags);
    }

    [Fact]
    public void Neighbouring_shops_below_the_office_threshold_are_kept()
    {
        // 5 shops in one market, 15 m apart — fewer than 8 other customers nearby.
        var points = new List<CustomerPaymentPoint>();
        for (var c = 1; c <= 5; c++)
        {
            for (var k = 0; k < 3; k++)
            {
                points.Add(P(c, c * 15 + k));
            }
        }

        var estimates = ShopLocationEstimator.Estimate(points);
        Assert.Equal(5, estimates.Count);
    }
}
