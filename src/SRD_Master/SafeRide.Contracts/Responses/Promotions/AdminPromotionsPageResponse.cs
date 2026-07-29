namespace SafeRide.Contracts.Responses.Promotions;

public sealed record AdminPromotionCountsResponse(
    int Total,
    int Active,
    int Inactive,
    int Expired);

public sealed record AdminPromotionsPageResponse(
    IReadOnlyList<AdminPromotionResponse> Items,
    AdminPromotionCountsResponse Counts,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
