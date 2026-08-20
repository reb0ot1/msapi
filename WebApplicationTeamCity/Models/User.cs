namespace WebApplicationTeamCity.Models;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string PasswordHash { get; set; }

    public required string Role { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
