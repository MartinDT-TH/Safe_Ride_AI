using SafeRide.Infrastructure.TripChat;

namespace SafeRide.UnitTests;

public sealed class TripChatContentFilterTests
{
    private readonly TripChatContentFilter _filter = new();

    [Theory]
    [InlineData("Đồ đéo biết lái xe", "Đồ *** biết lái xe")]
    [InlineData("You are an asshole", "You are an *******")]
    [InlineData("Đừng gọi người khác là mọi đen", "Đừng gọi người khác là *******")]
    public void Filter_WithUnsafeLanguage_MasksBlockedWording(
        string content,
        string expected)
    {
        Assert.Equal(expected, _filter.Filter(content));
    }

    [Theory]
    [InlineData("Bạn vui lòng đợi ở cổng chính")]
    [InlineData("The class starts at nine")]
    [InlineData("Tôi đồng ý")]
    public void Filter_WithSafeLanguage_KeepsOriginalContent(string content)
    {
        Assert.Equal(content, _filter.Filter(content));
    }
}
