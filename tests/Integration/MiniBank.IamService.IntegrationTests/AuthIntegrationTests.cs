using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniBank.IamService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace MiniBank.IamService.IntegrationTests;

public sealed class AuthIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("iam_test_db")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.UseSetting("ConnectionStrings:IamDb", _postgres.GetConnectionString());
                host.UseSetting("Jwt:Secret", "integration-test-secret-key-that-is-at-least-32-chars!");
                host.UseSetting("Jwt:Issuer", "MiniBank.IAM");
                host.UseSetting("Jwt:Audience", "MiniBank");
            });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Register_ShouldReturn201_WithValidRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "test@minibank.com",
            password = "SecurePassword123!",
            firstName = "Test",
            lastName = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be("test@minibank.com");
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_ShouldReturn409_WhenEmailDuplicate()
    {
        var payload = new
        {
            email = "duplicate@minibank.com",
            password = "Password123!",
            firstName = "A",
            lastName = "B"
        };
        await _client.PostAsJsonAsync("/api/auth/register", payload);

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_ShouldReturnTokens_WithValidCredentials()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "login@minibank.com",
            password = "Password123!",
            firstName = "Login",
            lastName = "User"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "login@minibank.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_ShouldReturn401_WithWrongPassword()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "badpass@minibank.com",
            password = "CorrectPassword123!",
            firstName = "A",
            lastName = "B"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "badpass@minibank.com",
            password = "WrongPassword!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ShouldReturnNewTokens_WithValidRefreshToken()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "refresh@minibank.com",
            password = "Password123!",
            firstName = "R",
            lastName = "U"
        });
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "refresh@minibank.com",
            password = "Password123!"
        });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginBody.GetProperty("refreshToken").GetString()!;

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().Should().NotBe(refreshToken);
    }

    [Fact]
    public async Task Health_ShouldReturn200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
