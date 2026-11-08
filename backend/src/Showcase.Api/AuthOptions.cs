namespace Showcase.Api;

public class AuthOptions
{
    public const string SessionVersionClaim = "sv";
    public const string LoginRateLimitPolicy = "login";

    /// <summary>Hash from <c>dotnet run -- hash-password</c>; set through the environment, never committed.</summary>
    public string AdminPasswordHash { get; set; } = "";

    /// <summary>Increase to invalidate every existing session cookie.</summary>
    public int SessionVersion { get; set; } = 1;

    public int LoginPermitPerMinute { get; set; } = 5;
}
