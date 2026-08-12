using System.ComponentModel.DataAnnotations;
using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Requests.Promotions;

public sealed class AdminPromotionRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã khuyến mãi.")]
    [StringLength(50, ErrorMessage = "Mã khuyến mãi không được vượt quá 50 ký tự.")]
    public string PromotionCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại khuyến mãi.")]
    public DiscountType? DiscountType { get; init; }

    public decimal DiscountValue { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    [Range(1, int.MaxValue, ErrorMessage = "Tổng lượt sử dụng phải lớn hơn 0.")]
    public int MaxUsageCount { get; init; }
    public decimal MinimumOrderValue { get; init; }
    public decimal MaximumDiscountValue { get; init; }
    [Range(1, int.MaxValue, ErrorMessage = "Giới hạn sử dụng mỗi người phải lớn hơn 0.")]
    public int UsageLimitPerUser { get; init; }
    [Range(0, int.MaxValue, ErrorMessage = "Số chuyến hoàn thành tối thiểu không được âm.")]
    public int? RequiredCompletedTrips { get; init; }
    public bool IsActive { get; init; }
}
