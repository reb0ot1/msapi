namespace WebApplicationTeamCity.Models;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public required string TokenHash { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}
