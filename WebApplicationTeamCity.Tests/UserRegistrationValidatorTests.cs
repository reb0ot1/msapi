using WebApplicationTeamCity.Contracts;
using WebApplicationTeamCity.Users;

namespace WebApplicationTeamCity.Tests;

public class UserRegistrationValidatorTests
{
    [Fact]
    public void Validate_valid_request_returns_no_errors()
    {
        var errors = UserRegistrationValidator.Validate(
            new CreateUserRequest("user@example.com", "Ada", "Lovelace"));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_invalid_email_returns_email_error(string? email)
    {
        var errors = UserRegistrationValidator.Validate(
            new CreateUserRequest(email, "Ada", "Lovelace"));

        Assert.Contains("email", errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_missing_name_returns_name_errors()
    {
        var errors = UserRegistrationValidator.Validate(
            new CreateUserRequest("user@example.com", " ", null));

        Assert.Contains("firstName", errors.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("lastName", errors.Keys, StringComparer.OrdinalIgnoreCase);
    }
}
