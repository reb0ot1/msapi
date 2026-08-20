using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplicationTeamCity.Contracts;
using WebApplicationTeamCity.Data;
using WebApplicationTeamCity.Models;

namespace WebApplicationTeamCity.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        JwtTokenService tokenService,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var user = string.IsNullOrWhiteSpace(email)
            ? null
            : await dbContext.Users.SingleOrDefaultAsync(
                candidate => candidate.Email == email,
                cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(request.Password)
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(await CreateAuthResponseAsync(
            user, dbContext, tokenService, authOptions.Value, cancellationToken));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> RefreshAsync(
        RefreshTokenRequest request,
        AppDbContext dbContext,
        JwtTokenService tokenService,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return TypedResults.Unauthorized();
        }

        var tokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null
            || storedToken.RevokedAtUtc.HasValue
            || storedToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return TypedResults.Unauthorized();
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        return TypedResults.Ok(await CreateAuthResponseAsync(
            storedToken.User, dbContext, tokenService, authOptions.Value, cancellationToken));
    }

    private static async Task<NoContent> LogoutAsync(
        RefreshTokenRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tokenHash = string.IsNullOrWhiteSpace(request.RefreshToken)
            ? null
            : JwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = tokenHash is null
            ? null
            : await dbContext.RefreshTokens.SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash && token.UserId == userId,
                cancellationToken);

        if (storedToken is not null)
        {
            storedToken.RevokedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AuthUserResponse>, NotFound>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new AuthUserResponse(
                user.Id, user.Email, user.FirstName, user.LastName, user.Role));
    }

    private static async Task<AuthResponse> CreateAuthResponseAsync(
        User user,
        AppDbContext dbContext,
        JwtTokenService tokenService,
        AuthOptions authOptions,
        CancellationToken cancellationToken)
    {
        var refreshToken = tokenService.CreateRefreshToken();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = JwtTokenService.HashRefreshToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(authOptions.RefreshTokenDays),
            UserId = user.Id
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresInSeconds) = tokenService.CreateAccessToken(user);
        return new AuthResponse(accessToken, refreshToken, expiresInSeconds);
    }
}
