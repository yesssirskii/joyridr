namespace Joyridr.Services;

public static class RidePricing
{
    public static decimal Calculate(decimal pricePerHour, int durationMinutes) =>
        Math.Round(pricePerHour * durationMinutes / 60m, 2, MidpointRounding.AwayFromZero);
}
