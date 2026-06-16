namespace SafeRide.Application.Features.Auth;

public static class PhoneNumberNormalizer
{
    public static string Normalize(string? phoneNumber)
    {
        var value = phoneNumber?.Trim() ?? string.Empty;
        if (value.Any(character =>
                !char.IsDigit(character) &&
                character is not '+' and not ' ' and not '(' and not ')' and not '.' and not '-'))
        {
            return string.Empty;
        }

        if (value.Count(character => character == '+') > 1 ||
            (value.Contains('+') && !value.StartsWith('+')))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        if (value.StartsWith('+'))
        {
            return NormalizeCompleteInternationalNumber(digits);
        }

        if (digits.Length == 9)
        {
            return $"+84{digits}";
        }

        if (digits.Length == 10 && digits.StartsWith('0'))
        {
            return $"+84{digits[1..]}";
        }

        return NormalizeCompleteInternationalNumber(digits);
    }

    public static bool IsValid(string? phoneNumber)
    {
        return !string.IsNullOrWhiteSpace(Normalize(phoneNumber));
    }

    private static string NormalizeCompleteInternationalNumber(string digits)
    {
        if (digits.Length < 9 || digits.Length > 15 || digits.StartsWith('0'))
        {
            return string.Empty;
        }

        if (digits.StartsWith("84") &&
            (digits.Length != 11 || digits.StartsWith("840")))
        {
            return string.Empty;
        }

        return $"+{digits}";
    }
}
