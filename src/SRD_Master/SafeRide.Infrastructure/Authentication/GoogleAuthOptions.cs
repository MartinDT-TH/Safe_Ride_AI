namespace SafeRide.Infrastructure.Authentication;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string[] ClientIds { get; init; } = [];
}