namespace SafeRide.Application.Features.Promotions;

internal static class PromotionUnlockRules
{
    public static void ValidateCompletedTrips(
        int customerCompletedTrips,
        int requiredCompletedTrips)
    {
        if (requiredCompletedTrips <= 0
            || customerCompletedTrips >= requiredCompletedTrips)
        {
            return;
        }

        var remainingTrips = requiredCompletedTrips - customerCompletedTrips;
        throw new PromotionException(
            "promotion.required_completed_trips_not_met",
            $"Bạn cần hoàn thành thêm {remainingTrips} chuyến để sử dụng mã khuyến mãi này.",
            400);
    }
}
