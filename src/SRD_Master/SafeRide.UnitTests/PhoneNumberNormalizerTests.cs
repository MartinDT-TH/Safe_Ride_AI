using SafeRide.Application.Features.Auth;

namespace SafeRide.UnitTests;

public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("0901234567", "+84901234567")]
    [InlineData("901234567", "+84901234567")]
    [InlineData("84901234567", "+84901234567")]
    [InlineData("+84901234567", "+84901234567")]
    [InlineData("+14155552671", "+14155552671")]
    public void Normalize_AcceptedFormats_ReturnsE164(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumberNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("01234567890")]
    [InlineData("+840901234567")]
    [InlineData("+0123456789")]
    [InlineData("abc0901234567")]
    public void Normalize_InvalidFormats_ReturnsEmpty(string input)
    {
        Assert.Equal(string.Empty, PhoneNumberNormalizer.Normalize(input));
    }
}
