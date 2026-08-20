using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebApplicationTeamCity.Contracts;
using WebApplicationTeamCity.Data;
using WebApplicationTeamCity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using WebApplicationTeamCity.Auth;

namespace WebApplicationTeamCity.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/users", CreateUserAsync);
        endpoints.MapGet("/users", GetUsersAsync).RequireAuthorization();
        endpoints.MapDelete("/users/{id:guid}", DeleteUserAsync)
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin));
        return endpoints;
    }

    private static async Task<Ok<List<UserResponse>>> GetUsersAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new UserResponse(
                user.Id, user.Email, user.FirstName, user.LastName, user.Role))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(users);
    }

    private static async Task<Results<Created<UserResponse>, ValidationProblem, Conflict<ProblemDetails>, UnauthorizedHttpResult>> CreateUserAsync(
        CreateUserRequest request,
        [FromHeader(Name = "X-Registration-Key")] string? registrationKey,
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(registrationKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(registrationKey),
                Encoding.UTF8.GetBytes(authOptions.Value.RegistrationSecretKey)))
        {
            return TypedResults.Unauthorized();
        }

        var errors = UserRegistrationValidator.Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var normalizedEmail = request.Email!.Trim().ToLowerInvariant();
        var emailExists = await dbContext.Users
            .AnyAsync(user => user.Email.ToLower() == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Email already exists.",
                Detail = "A user with this email address is already registered."
            });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            PasswordHash = string.Empty,
            Role = UserRoles.User
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);

        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Email already exists.",
                Detail = "A user with this email address is already registered."
            });
        }

        var response = new UserResponse(
            user.Id, user.Email, user.FirstName, user.LastName, user.Role);
        return TypedResults.Created($"/users/{user.Id}", response);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteUserAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role);
