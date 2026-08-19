using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        var createResponse = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest("user@example.com", "Ada", "Lovelace"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var users = await client.GetFromJsonAsync<List<UserResponse>>("/users");

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
        var request = new CreateUserRequest("duplicate@example.com", "Ada", "Lovelace");

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/users", request)).StatusCode);

        var duplicateResponse = await client.PostAsJsonAsync("/users", request);

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
                LastName = "User"
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Email = "second@example.com",
                FirstName = "Second",
                LastName = "User"
            });

        using var client = factory.CreateClient();
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

        var response = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest("not-an-email", "Ada", "Lovelace"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"users-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
        await dbContext.Users.AddRangeAsync(users);
        await dbContext.SaveChangesAsync();
    }
}
