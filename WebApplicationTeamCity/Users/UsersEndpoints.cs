using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebApplicationTeamCity.Contracts;
using WebApplicationTeamCity.Data;
using WebApplicationTeamCity.Models;

namespace WebApplicationTeamCity.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/users", CreateUserAsync);
        endpoints.MapGet("/users", GetUsersAsync);
        return endpoints;
    }

    private static async Task<Ok<List<UserResponse>>> GetUsersAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new UserResponse(user.Id, user.Email, user.FirstName, user.LastName))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(users);
    }

    private static async Task<Results<Created<UserResponse>, ValidationProblem, Conflict<ProblemDetails>>> CreateUserAsync(
        CreateUserRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
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
            LastName = request.LastName!.Trim()
        };

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

        var response = new UserResponse(user.Id, user.Email, user.FirstName, user.LastName);
        return TypedResults.Created($"/users/{user.Id}", response);
    }
}

public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName);
