using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Reports;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminReports.Commands.UpdateAdminReportStatus;

public sealed class UpdateAdminReportStatusCommandHandler
    : IRequestHandler<UpdateAdminReportStatusCommand, AdminReportResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAdminReportStatusCommandHandler(
        IReportRepository reportRepository,
        IUnitOfWork unitOfWork)
    {
        _reportRepository = reportRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminReportResponse> Handle(
        UpdateAdminReportStatusCommand request,
        CancellationToken cancellationToken)
    {
        var status = ParseStatus(request.Status);
        var report = await _reportRepository.GetReportForUpdateAsync(
            request.ReportId,
            cancellationToken)
            ?? throw new ReportException(
                "report.not_found",
                "Không tìm thấy báo cáo.",
                404);

        report.Status = status;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _reportRepository.GetAdminReportByIdAsync(
            report.Id,
            cancellationToken)
            ?? throw new ReportException(
                "report.not_found",
                "Không tìm thấy báo cáo.",
                404);
    }

    private static ReportStatus ParseStatus(string status)
    {
        if (Enum.TryParse<ReportStatus>(status?.Trim(), true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ReportException(
            "report.invalid_status",
            "Trạng thái báo cáo không hợp lệ. Chỉ chấp nhận Pending, Resolved hoặc Rejected.",
            400);
    }
}
