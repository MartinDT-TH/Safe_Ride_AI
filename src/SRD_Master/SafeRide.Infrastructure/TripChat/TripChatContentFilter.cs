using System.Text.RegularExpressions;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.TripChat;

public sealed partial class TripChatContentFilter : ITripChatContentFilter
{
    private static readonly string[] BlockedPatterns =
    [
        @"đ[ịi]t",
        @"đ[ụu]",
        @"đ[eé]o",
        @"l[oồ]n",
        @"c[ặa]c",
        @"đ[ĩi]",
        @"đm+",
        @"dmm?",
        @"m[ọo]i\s+đen",
        @"th[ằa]ng\s+(?:gay|b[êe]\s*đ[êe])",
        @"fuck(?:er|ing)?",
        @"shit(?:ty)?",
        @"bitch",
        @"asshole",
        @"bastard",
        @"nigg(?:er|a)",
        @"faggot",
        @"retard(?:ed)?",
        @"chink",
        @"씨발",
        @"병신",
        @"クソ",
        @"死ね",
        @"操你妈",
        @"傻逼"
    ];

    private static readonly Regex UnsafeLanguageRegex = new(
        $@"(?<![\p{{L}}\p{{N}}])(?:{string.Join("|", BlockedPatterns)})(?![\p{{L}}\p{{N}}])",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    public string Filter(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        return UnsafeLanguageRegex.Replace(
            content,
            match => new string('*', match.Length));
    }
}
