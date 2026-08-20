using System.ComponentModel.DataAnnotations;
using WebApplicationTeamCity.Contracts;

namespace WebApplicationTeamCity.Users;

public static class UserRegistrationValidator
{
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public static Dictionary<string, string[]> Validate(CreateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (!EmailAddressValidator.IsValid(email))
        {
            errors["email"] = ["Email must be a valid email address."];
        }

        ValidateName(request.FirstName, "firstName", errors);
        ValidateName(request.LastName, "lastName", errors);
        ValidatePassword(request.Password, errors);

        return errors;
    }

    private static void ValidateName(
        string? value,
        string fieldName,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[fieldName] = [$"{fieldName} is required."];
        }
        else if (value.Trim().Length > 100)
        {
            errors[fieldName] = [$"{fieldName} must be 100 characters or fewer."];
        }
    }

    private static void ValidatePassword(
        string? password,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            errors["password"] = ["Password is required."];
        }
        else if (password.Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters long."];
        }
    }
}
