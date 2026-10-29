using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Numerics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace MiniBank.AccountService.IntegrationTests;

public sealed class AccountIntegrationTests : IAsyncLifetime
{
    private const string JwtSecret = "integration-test-secret-key-that-is-at-least-32-chars!";
    private const string JwtIssuer = "MiniBank.IAM";
    private const string JwtAudience = "MiniBank";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("account_test_db")
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
                host.UseSetting("ConnectionStrings:AccountDb", _postgres.GetConnectionString());
                host.UseSetting("Jwt:Secret", JwtSecret);
                host.UseSetting("Jwt:Issuer", JwtIssuer);
                host.UseSetting("Jwt:Audience", JwtAudience);
            });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateToken());
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static string GenerateToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "test@minibank.com"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Random 26-char TR IBAN with valid MOD-97 check digits ("2927" = "TR" converted to digits)
    private static string NewIban()
    {
        var bban = string.Concat(Enumerable.Range(0, 22).Select(_ => Random.Shared.Next(10)));
        var checkDigits = 98 - (int)(BigInteger.Parse(bban + "292700") % 97);
        return $"TR{checkDigits:D2}{bban}";
    }

    [Fact]
    public async Task CreateAccount_ShouldReturn201_WithValidRequest()
    {
        var customerId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = customerId.ToString(),
            iban = NewIban(),
            currency = "TRY",
            accountType = "Checking"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("customerId").GetString().Should().Be(customerId.ToString());
        body.GetProperty("balance").GetDecimal().Should().Be(0m);
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Deposit_ShouldIncreaseBalance()
    {
        var customerId = Guid.NewGuid().ToString();
        var createResp = await _client.PostAsJsonAsync("/api/accounts", new
        {
            customerId,
            iban = NewIban(),
            currency = "TRY",
            accountType = "Savings"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetString();

        var depositResp = await _client.PostAsJsonAsync($"/api/accounts/{accountId}/deposit", new
        {
            amount = 500m,
            description = "Initial deposit"
        });

        depositResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await depositResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("balance").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task Withdraw_ShouldDecreaseBalance()
    {
        var createResp = await _client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = Guid.NewGuid().ToString(),
            iban = NewIban(),
            currency = "TRY",
            accountType = "Checking"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetString();

        await _client.PostAsJsonAsync($"/api/accounts/{accountId}/deposit", new
        {
            amount = 1000m,
            description = "Fund account"
        });

        var withdrawResp = await _client.PostAsJsonAsync($"/api/accounts/{accountId}/withdraw", new
        {
            amount = 300m,
            description = "ATM withdrawal"
        });

        withdrawResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await withdrawResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("balance").GetDecimal().Should().Be(700m);
    }

    [Fact]
    public async Task Withdraw_ShouldReturn500_WhenInsufficientFunds()
    {
        var createResp = await _client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = Guid.NewGuid().ToString(),
            iban = NewIban(),
            currency = "TRY",
            accountType = "Checking"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetString();

        var response = await _client.PostAsJsonAsync($"/api/accounts/{accountId}/withdraw", new
        {
            amount = 9999m,
            description = "Overdraft attempt"
        });

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Freeze_ShouldChangeStatusToFrozen()
    {
        var createResp = await _client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = Guid.NewGuid().ToString(),
            iban = NewIban(),
            currency = "TRY",
            accountType = "Checking"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetString();

        var freezeResp = await _client.PostAsJsonAsync($"/api/accounts/{accountId}/freeze", new { });

        freezeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await freezeResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Frozen");
    }

    [Fact]
    public async Task GetLedger_ShouldReturnEntries_AfterDepositAndWithdrawal()
    {
        var createResp = await _client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = Guid.NewGuid().ToString(),
            iban = NewIban(),
            currency = "TRY",
            accountType = "Savings"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetString();

        await _client.PostAsJsonAsync($"/api/accounts/{accountId}/deposit",
            new { amount = 1000m, description = "deposit" });
        await _client.PostAsJsonAsync($"/api/accounts/{accountId}/withdraw",
            new { amount = 200m, description = "withdraw" });

        var ledgerResp = await _client.GetAsync($"/api/accounts/{accountId}/ledger");
        ledgerResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await ledgerResp.Content.ReadFromJsonAsync<JsonElement[]>();
        entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_ShouldReturn404_ForNonExistentAccount()
    {
        var response = await _client.GetAsync($"/api/accounts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_ShouldReturn200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
