using Joyridr.Common;
using Joyridr.Services;

namespace Joyridr.Tests;

public class GeoAndPricingTests
{
    [Fact]
    public void Distance_between_Zagreb_and_Split_is_about_260_km()
    {
        var km = Geo.DistanceKm(45.8150, 15.9819, 43.5081, 16.4402);

        Assert.InRange(km, 255, 265);
    }

    [Fact]
    public void Bounding_box_contains_points_inside_the_radius()
    {
        var (minLat, maxLat, minLng, maxLng) = Geo.BoundingBox(45.8150, 15.9819, 25);

        // Samobor is ~20 km west of Zagreb.
        Assert.InRange(45.8011, minLat, maxLat);
        Assert.InRange(15.7110, minLng, maxLng);
    }

    [Theory]
    [InlineData(180, 60, 180)]
    [InlineData(180, 90, 270)]
    [InlineData(99.99, 30, 50.00)] // 49.995 rounds up
    public void Price_is_hourly_rate_times_duration(decimal pricePerHour, int minutes, decimal expected)
    {
        Assert.Equal(expected, RidePricing.Calculate(pricePerHour, minutes));
    }
}
