namespace WebApplicationTeamCity.Contracts;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds);

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role);
