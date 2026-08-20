namespace WebApplicationTeamCity.Contracts;

public sealed record CreateUserRequest(
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password);
