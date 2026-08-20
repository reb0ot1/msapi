namespace WebApplicationTeamCity.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Authentication";

    public required string JwtKey { get; init; }

    public required string JwtIssuer { get; init; }

    public required string JwtAudience { get; init; }

    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 30;

    public required string RegistrationSecretKey { get; init; }
}
