using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using WebApplicationTeamCity.Contracts;
using WebApplicationTeamCity.Data;
using WebApplicationTeamCity.Models;
using WebApplicationTeamCity.Users;

namespace WebApplicationTeamCity.Tests;

public sealed class UserEndpointsTests
{
    [Fact]
    public async Task Post_user_then_get_users_returns_created_user()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await RegisterAsync(
            client,
            "/users",
            new CreateUserRequest("user@example.com", "Ada", "Lovelace", "password123"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var authenticatedClient = await LoginAsync(factory, "user@example.com", "password123");
        var users = await authenticatedClient.GetFromJsonAsync<List<UserResponse>>("/users");

        var user = Assert.Single(users!);
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
    }

    [Fact]
    public async Task Post_duplicate_email_returns_conflict()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = new CreateUserRequest("duplicate@example.com", "Ada", "Lovelace", "password123");

        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(client, "/users", request)).StatusCode);

        var duplicateResponse = await RegisterAsync(client, "/users", request);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Get_users_returns_existing_users()
    {
        using var factory = new ApiFactory();
        await factory.SeedUsersAsync(
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Email = "first@example.com",
                FirstName = "First",
                LastName = "User",
                PasswordHash = "hash",
                Role = UserRoles.User
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Email = "second@example.com",
                FirstName = "Second",
                LastName = "User",
                PasswordHash = "hash",
                Role = UserRoles.User
            });

        using var client = await LoginAsync(factory, "first@example.com", "password123");
        var users = await client.GetFromJsonAsync<List<UserResponse>>("/users");

        Assert.Equal(
            ["first@example.com", "second@example.com"],
            users!.Select(user => user.Email).ToArray());
    }

    [Fact]
    public async Task Post_invalid_email_returns_bad_request()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await RegisterAsync(
            client,
            "/users",
            new CreateUserRequest("not-an-email", "Ada", "Lovelace", "password123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_users_without_token_returns_unauthorized()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registration_without_secret_key_returns_unauthorized()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest("user@example.com", "Ada", "Lovelace", "password123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_me_returns_user()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createResponse = await RegisterAsync(
            client,
            "/users",
            new CreateUserRequest("user@example.com", "Ada", "Lovelace", "password123"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest("user@example.com", "password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var refreshResponse = await client.PostAsJsonAsync(
            "/auth/refresh", new RefreshTokenRequest(login!.RefreshToken));
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        var me = await client.GetFromJsonAsync<AuthUserResponse>("/auth/me");

        Assert.Equal("user@example.com", me!.Email);
    }

    [Fact]
    public async Task Only_admin_can_delete_users()
    {
        using var factory = new ApiFactory();
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var adminId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await factory.SeedUsersAsync(
            new User
            {
                Id = userId,
                Email = "normal@example.com",
                FirstName = "Normal",
                LastName = "User",
                PasswordHash = "hash",
                Role = UserRoles.User
            },
            new User
            {
                Id = adminId,
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                PasswordHash = "hash",
                Role = UserRoles.Admin
            });

        using var normalClient = await LoginAsync(factory, "normal@example.com", "password123");
        var forbidden = await normalClient.DeleteAsync($"/users/{adminId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var adminClient = await LoginAsync(factory, "admin@example.com", "password123");
        var deleted = await adminClient.DeleteAsync($"/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var missing = await adminClient.DeleteAsync($"/users/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string path,
        CreateUserRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path);
        message.Headers.Add("X-Registration-Key", ApiFactory.RegistrationSecretKey);
        message.Content = JsonContent.Create(request);
        return await client.SendAsync(message);
    }

    private static async Task<HttpClient> LoginAsync(
        ApiFactory factory,
        string email,
        string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string RegistrationSecretKey = "test-registration-key";
    private readonly string databaseName = $"users-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:JwtKey", "test-jwt-key-that-is-at-least-32-characters");
        builder.UseSetting("Authentication:JwtIssuer", "test-issuer");
        builder.UseSetting("Authentication:JwtAudience", "test-audience");
        builder.UseSetting("Authentication:RegistrationSecretKey", RegistrationSecretKey);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        });
    }

    public async Task SeedUsersAsync(params User[] users)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var passwordHasher = new PasswordHasher<User>();
        foreach (var user in users.Where(user => user.PasswordHash == "hash"))
        {
            user.PasswordHash = passwordHasher.HashPassword(user, "password123");
        }
        await dbContext.Users.AddRangeAsync(users);
        await dbContext.SaveChangesAsync();
    }
}
