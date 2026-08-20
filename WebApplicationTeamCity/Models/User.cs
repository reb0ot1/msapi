namespace WebApplicationTeamCity.Models;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }
}
