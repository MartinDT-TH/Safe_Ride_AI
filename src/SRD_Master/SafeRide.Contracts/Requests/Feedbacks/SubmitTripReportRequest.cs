using System.ComponentModel.DataAnnotations;

namespace SafeRide.Contracts.Requests.Feedbacks;

public sealed class SubmitTripReportRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề báo cáo.")]
    [StringLength(255, ErrorMessage = "Tiêu đề báo cáo quá dài.")]
    public string Subject { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung báo cáo.")]
    [StringLength(1000, ErrorMessage = "Nội dung báo cáo quá dài.")]
    public string Description { get; init; } = string.Empty;
}
