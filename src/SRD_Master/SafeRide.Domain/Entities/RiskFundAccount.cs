namespace SafeRide.Domain.Entities;

public sealed class RiskFundAccount
{
    public long Id { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<RiskFundTransaction> Transactions { get; set; } = new List<RiskFundTransaction>();
}
