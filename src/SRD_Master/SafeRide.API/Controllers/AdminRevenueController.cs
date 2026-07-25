using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/revenue")]
public sealed class AdminRevenueController : ControllerBase
{
    private const decimal PlatformShareRate = 0.30m;
    private readonly ApplicationDbContext _db;

    public AdminRevenueController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<AdminRevenueResponse>> GetRevenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var endDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = from ?? endDate.AddDays(-29);
        if (startDate > endDate)
            return BadRequest(new { message = "Ngày bắt đầu không được sau ngày kết thúc." });
        if (endDate.DayNumber - startDate.DayNumber > 365)
            return BadRequest(new { message = "Khoảng thời gian tối đa là 366 ngày." });

        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var days = endDate.DayNumber - startDate.DayNumber + 1;
        var previousStart = start.AddDays(-days);
        var payments = _db.Payments.AsNoTracking()
            .Where(x => x.PaymentStatus == PaymentStatus.Success && x.PaidAt != null);

        var currentRows = await payments
            .Where(x => x.PaidAt >= start && x.PaidAt < endExclusive)
            .Select(x => new RevenueRow(x.TripId, x.Amount, x.PaidAt!.Value.Date,
                x.Trip.Booking.ServiceType.ServiceName))
            .ToListAsync(cancellationToken);
        var previousRevenue = await payments
            .Where(x => x.PaidAt >= previousStart && x.PaidAt < start)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var previousTrips = await payments
            .Where(x => x.PaidAt >= previousStart && x.PaidAt < start)
            .Select(x => x.TripId).Distinct().CountAsync(cancellationToken);

        var totalRevenue = currentRows.Sum(x => x.Amount);
        var successfulTrips = currentRows.Select(x => x.TripId).Distinct().Count();
        var byDate = currentRows.GroupBy(x => DateOnly.FromDateTime(x.PaidDate))
            .ToDictionary(x => x.Key, x => x.Sum(row => row.Amount));
        var timeline = Enumerable.Range(0, days).Select(startDate.AddDays)
            .Select(date => new RevenueTimelinePoint(date, byDate.GetValueOrDefault(date))).ToArray();
        var services = currentRows.GroupBy(x => x.ServiceName)
            .Select(group => new RevenueServiceBreakdown(group.Key, group.Sum(x => x.Amount),
                group.Select(x => x.TripId).Distinct().Count(),
                totalRevenue == 0 ? 0 : Math.Round(group.Sum(x => x.Amount) / totalRevenue * 100m, 2)))
            .OrderByDescending(x => x.Revenue).ToArray();

        return Ok(new AdminRevenueResponse(startDate, endDate, totalRevenue, successfulTrips,
            Math.Round(totalRevenue * PlatformShareRate, 0),
            CalculateGrowth(totalRevenue, previousRevenue), CalculateGrowth(successfulTrips, previousTrips),
            timeline, services));
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportRevenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string format = "xlsx", [FromQuery] string report = "revenue",
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(report, "revenue", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Loại báo cáo không được hỗ trợ." });
        format = format.ToLowerInvariant();
        if (format is not ("xlsx" or "csv"))
            return BadRequest(new { message = "Định dạng tệp phải là XLSX hoặc CSV." });

        var endDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = from ?? endDate.AddDays(-29);
        if (startDate > endDate)
            return BadRequest(new { message = "Ngày bắt đầu không được sau ngày kết thúc." });
        if (endDate.DayNumber - startDate.DayNumber > 365)
            return BadRequest(new { message = "Khoảng thời gian tối đa là 366 ngày." });

        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rows = await _db.Payments.AsNoTracking()
            .Where(x => x.PaymentStatus == PaymentStatus.Success && x.PaidAt >= start && x.PaidAt < endExclusive)
            .OrderByDescending(x => x.PaidAt)
            .Select(x => new RevenueExportRow(x.PaidAt!.Value, x.TripId,
                x.Trip.Booking.ServiceType.ServiceName, x.PaymentMethod.ToString(), x.Amount,
                Math.Round(x.Amount * PlatformShareRate, 0)))
            .ToListAsync(cancellationToken);

        var fileName = $"Bao_cao_doanh_thu_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.{format}";
        if (format == "csv")
            return File(BuildCsv(rows), "text/csv; charset=utf-8", fileName);
        return File(BuildXlsx(rows, startDate, endDate),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static decimal? CalculateGrowth(decimal current, decimal previous) =>
        previous == 0 ? current == 0 ? 0 : null : Math.Round((current - previous) / previous * 100m, 2);

    private sealed record RevenueRow(long TripId, decimal Amount, DateTime PaidDate, string ServiceName);
    private sealed record RevenueExportRow(DateTime PaidAt, long TripId, string ServiceName,
        string PaymentMethod, decimal Revenue, decimal PlatformFee);

    private static byte[] BuildCsv(IReadOnlyCollection<RevenueExportRow> rows)
    {
        var csv = new StringBuilder("Ngày thanh toán,Mã chuyến,Dịch vụ,Phương thức,Tổng doanh thu,Phí nền tảng\r\n");
        foreach (var row in rows)
            csv.Append(Csv(row.PaidAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))).Append(',')
                .Append("TRP-").Append(row.TripId).Append(',').Append(Csv(row.ServiceName)).Append(',')
                .Append(Csv(row.PaymentMethod)).Append(',').Append(row.Revenue.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.PlatformFee.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        return new UTF8Encoding(true).GetBytes(csv.ToString());
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static byte[] BuildXlsx(IReadOnlyList<RevenueExportRow> rows, DateOnly from, DateOnly to)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            WriteEntry(archive, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            WriteEntry(archive, "xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Doanh thu" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            WriteEntry(archive, "xl/styles.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF007682"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="2"><xf xfId="0"/><xf xfId="0" fontId="1" fillId="2" applyFont="1" applyFill="1"/></cellXfs></styleSheet>""");
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(rows, from, to));
        }
        return stream.ToArray();
    }

    private static string BuildSheet(IReadOnlyList<RevenueExportRow> rows, DateOnly from, DateOnly to)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"2\" topLeftCell=\"A3\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><cols><col min=\"1\" max=\"1\" width=\"21\" customWidth=\"1\"/><col min=\"2\" max=\"4\" width=\"19\" customWidth=\"1\"/><col min=\"5\" max=\"6\" width=\"18\" customWidth=\"1\"/></cols><sheetData>");
        AddRow(xml, 1, new[] { $"BÁO CÁO DOANH THU SAFERIDE ({from:dd/MM/yyyy} - {to:dd/MM/yyyy})" }, 1);
        AddRow(xml, 2, new[] { "Ngày thanh toán", "Mã chuyến", "Dịch vụ", "Phương thức", "Tổng doanh thu (VND)", "Phí nền tảng (VND)" }, 1);
        var index = 3;
        foreach (var row in rows)
            AddRow(xml, index++, new[] { row.PaidAt.ToString("dd/MM/yyyy HH:mm"), $"TRP-{row.TripId}", row.ServiceName, row.PaymentMethod, row.Revenue.ToString("0", CultureInfo.InvariantCulture), row.PlatformFee.ToString("0", CultureInfo.InvariantCulture) }, 0);
        xml.Append("</sheetData><autoFilter ref=\"A2:F").Append(Math.Max(2, index - 1)).Append("\"/></worksheet>");
        return xml.ToString();
    }

    private static void AddRow(StringBuilder xml, int rowNumber, IEnumerable<string> cells, int style)
    {
        xml.Append("<row r=\"").Append(rowNumber).Append("\">");
        foreach (var value in cells)
            xml.Append("<c t=\"inlineStr\" s=\"").Append(style).Append("\"><is><t>")
                .Append(SecurityElement.Escape(value)).Append("</t></is></c>");
        xml.Append("</row>");
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}

public sealed record AdminRevenueResponse(DateOnly From, DateOnly To, decimal TotalRevenue,
    int SuccessfulTrips, decimal PlatformFee, decimal? RevenueGrowthPercent, decimal? TripsGrowthPercent,
    IReadOnlyCollection<RevenueTimelinePoint> Timeline, IReadOnlyCollection<RevenueServiceBreakdown> Services);
public sealed record RevenueTimelinePoint(DateOnly Date, decimal Revenue);
public sealed record RevenueServiceBreakdown(string ServiceName, decimal Revenue, int Trips, decimal Percentage);
