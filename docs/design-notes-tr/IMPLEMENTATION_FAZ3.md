# FAZ 3 — ACCOUNT SERVICE (Müşteri & Hesap Yönetimi)

> **ÖN KOŞUL:** Faz 0, 1, 2 tamamlanmış. IAM Service'ten JWT alınabiliyor.
> **PORT:** 5002
> **VERİTABANI:** account_db

**AMAÇ:** Müşteri oluşturma, hesap açma/kapatma/dondurma, bakiye sorgulama. Her müşterinin birden fazla hesabı olabilir (TRY vadesiz, USD tasarruf, vb.)

**BU FAZIN SONUNDA:**
- `POST /api/customers` → yeni müşteri kaydı (KYC stub ile)
- `POST /api/accounts` → yeni hesap açma
- `GET /api/accounts/{id}` → hesap ve bakiye bilgisi
- `PUT /api/accounts/{id}/freeze` → hesap dondurma
- Customer-Account one-to-many ilişkisi çalışıyor
- JWT authentication zorunlu (IAM token'ı ile erişilir)

---

## ADIM 3.0 — Klasör Yapısı ve NuGet Paketleri

**KOMUTLAR:**

```powershell
mkdir src\Services\MiniBank.AccountService\Domain
mkdir src\Services\MiniBank.AccountService\Domain\Entities
mkdir src\Services\MiniBank.AccountService\Domain\Events
mkdir src\Services\MiniBank.AccountService\Infrastructure
mkdir src\Services\MiniBank.AccountService\Infrastructure\Persistence
mkdir src\Services\MiniBank.AccountService\Infrastructure\ExternalServices
mkdir src\Services\MiniBank.AccountService\Application
mkdir src\Services\MiniBank.AccountService\Api
mkdir src\Services\MiniBank.AccountService\Api\Controllers
mkdir src\Services\MiniBank.AccountService\Api\Dtos

dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package Microsoft.EntityFrameworkCore
dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package System.IdentityModel.Tokens.Jwt
dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package Microsoft.IdentityModel.Tokens
dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package Swashbuckle.AspNetCore
dotnet add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj package Serilog.AspNetCore
```

- [ ] Tamamlandı

---

## ADIM 3.1 — Customer Entity

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Domain\Entities\Customer.cs`

```csharp
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.AccountService.Domain.Entities;

/// <summary>
/// Banka müşterisi. Bireysel veya kurumsal olabilir.
/// Her müşterinin:
/// - TC Kimlik No'su var (NationalId)
/// - Birden fazla hesabı olabilir (1:N ilişki)
/// - KYC (Know Your Customer) onayı gerekli
///
/// KVKK kapsamında kişisel veri içerir — loglarda maskelenmeli.
/// </summary>
public sealed class Customer : AggregateRoot<CustomerId>, IAggregateWithEvents
{
    public NationalId NationalId { get; private set; } = default!;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public DateTime DateOfBirth { get; private set; }
    public bool KycVerified { get; private set; }
    public DateTime? KycVerifiedAt { get; private set; }

    private readonly List<Account> _accounts = [];
    public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

    private Customer() { }

    public static Customer Create(
        NationalId nationalId,
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        DateTime dateOfBirth)
    {
        var customer = new Customer
        {
            Id = CustomerId.New(),
            NationalId = nationalId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phoneNumber.Trim(),
            DateOfBirth = dateOfBirth,
            KycVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };

        return customer;
    }

    /// <summary>
    /// KYC doğrulaması yapıldı olarak işaretle.
    /// Gerçek sistemde KYC sağlayıcıdan onay geldikten sonra çağrılır.
    /// Bu projede stub/simulator kullanıyoruz.
    /// </summary>
    public void MarkKycVerified()
    {
        KycVerified = true;
        KycVerifiedAt = DateTime.UtcNow;
        MarkModified();
    }

    /// <summary>
    /// Müşteriye yeni hesap ekle.
    /// KYC onayı zorunlu — onay olmadan hesap açılamaz (BDDK zorunluluğu).
    /// </summary>
    public Account OpenAccount(AccountType type, Currency currency)
    {
        if (!KycVerified)
            throw new InvalidOperationException("Cannot open account without KYC verification.");

        var account = Account.Create(Id, type, currency);
        _accounts.Add(account);
        MarkModified();
        return account;
    }

    public string FullName => $"{FirstName} {LastName}";

    // IAggregateWithEvents implementasyonu
    IReadOnlyCollection<object> IAggregateWithEvents.GetDomainEvents()
        => DomainEvents.Cast<object>().ToList().AsReadOnly();

    void IAggregateWithEvents.ClearEvents() => ClearDomainEvents();
}
```

- [ ] Tamamlandı

---

## ADIM 3.2 — Account Entity

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Domain\Entities\Account.cs`

```csharp
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.AccountService.Domain.Entities;

/// <summary>
/// Banka hesabı. Bir müşterinin birden fazla hesabı olabilir.
/// Her hesabın:
/// - Bir IBAN'ı var (otomatik üretilir)
/// - Bir bakiyesi var (Money value object)
/// - Bir durumu var (Active, Frozen, Closed, Dormant)
/// - Bir tipi var (Checking, Savings, Current, Credit)
/// </summary>
public sealed class Account : Entity<AccountId>
{
    public CustomerId CustomerId { get; private set; } = default!;
    public string AccountNumber { get; private set; } = string.Empty;
    public string IbanNumber { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public AccountStatus Status { get; private set; }
    public Currency Currency { get; private set; }
    public decimal Balance { get; private set; }

    private Account() { }

    internal static Account Create(CustomerId customerId, AccountType type, Currency currency)
    {
        var accountNumber = GenerateAccountNumber();

        var account = new Account
        {
            Id = AccountId.New(),
            CustomerId = customerId,
            AccountNumber = accountNumber,
            IbanNumber = GenerateIban(accountNumber),
            Type = type,
            Status = AccountStatus.Active,
            Currency = currency,
            Balance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };

        return account;
    }

    /// <summary>
    /// Hesap bakiyesini artırır (para yatırma, gelen havale, vb.)
    /// </summary>
    public void Credit(decimal amount)
    {
        EnsureActive();
        if (amount <= 0)
            throw new ArgumentException("Credit amount must be positive.", nameof(amount));

        Balance += amount;
        MarkModified();
    }

    /// <summary>
    /// Hesap bakiyesini azaltır (para çekme, giden havale, vb.)
    /// </summary>
    public void Debit(decimal amount)
    {
        EnsureActive();
        if (amount <= 0)
            throw new ArgumentException("Debit amount must be positive.", nameof(amount));
        if (Balance < amount)
            throw new InsufficientFundsException(
                new Money(Balance, Currency),
                new Money(amount, Currency));

        Balance -= amount;
        MarkModified();
    }

    /// <summary>
    /// Hesabı dondurur. MASAK veya mahkeme kararı ile yapılır.
    /// Donmuş hesaptan para çıkamaz, ama yatırılabilir (yasal düzenleme).
    /// </summary>
    public void Freeze()
    {
        if (Status == AccountStatus.Closed)
            throw new InvalidStateTransitionException("Account", Status.ToString(), AccountStatus.Frozen.ToString());

        Status = AccountStatus.Frozen;
        MarkModified();
    }

    /// <summary>
    /// Hesap dondurmasını kaldırır.
    /// </summary>
    public void Unfreeze()
    {
        if (Status != AccountStatus.Frozen)
            throw new InvalidStateTransitionException("Account", Status.ToString(), AccountStatus.Active.ToString());

        Status = AccountStatus.Active;
        MarkModified();
    }

    /// <summary>
    /// Hesabı kapatır. Bakiye 0 olmalı.
    /// </summary>
    public void Close()
    {
        if (Balance != 0)
            throw new InvalidOperationException("Account balance must be zero before closing.");

        Status = AccountStatus.Closed;
        MarkModified();
    }

    private void EnsureActive()
    {
        if (Status == AccountStatus.Frozen)
            throw new AccountFrozenException(Id.Value.ToString());
        if (Status == AccountStatus.Closed)
            throw new InvalidOperationException("Account is closed.");
    }

    private static string GenerateAccountNumber()
    {
        return Random.Shared.NextInt64(1000000000000000, 9999999999999999).ToString();
    }

    private static string GenerateIban(string accountNumber)
    {
        return $"TR00 0001 0000 {accountNumber[..4]} {accountNumber[4..8]} {accountNumber[8..12]} {accountNumber[12..]}";
    }
}
```

- [ ] Tamamlandı

---

## ADIM 3.3 — KYC Provider Stub

**NE YAPILACAK:** Gerçek KYC sağlayıcı (Nüfus Müdürlüğü API vb.) yerine stub. Interface tanımlanır, stub always-approve döner. Production'da gerçek provider geldiğinde sadece implementasyon değişir.

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Application\IKycProvider.cs`

```csharp
namespace MiniBank.AccountService.Application;

public interface IKycProvider
{
    Task<KycResult> VerifyCustomerAsync(string nationalId, string firstName, string lastName, DateTime dateOfBirth, CancellationToken ct);
}

public sealed record KycResult(bool IsVerified, string? RejectionReason = null);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Infrastructure\ExternalServices\StubKycProvider.cs`

```csharp
using MiniBank.AccountService.Application;

namespace MiniBank.AccountService.Infrastructure.ExternalServices;

/// <summary>
/// STUB: Gerçek KYC doğrulaması yapılmaz — her zaman onaylar.
/// Production'da bu class yerine gerçek implementasyon gelecek.
/// Interface (IKycProvider) değişmez.
/// </summary>
public sealed class StubKycProvider : IKycProvider
{
    public Task<KycResult> VerifyCustomerAsync(
        string nationalId,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        CancellationToken ct)
    {
        return Task.FromResult(new KycResult(IsVerified: true));
    }
}
```

- [ ] Tamamlandı

---

## ADIM 3.4 — Account DbContext

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Infrastructure\Persistence\AccountDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using MiniBank.AccountService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.AccountService.Infrastructure.Persistence;

public sealed class AccountDbContext : BaseDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();

    public AccountDbContext(DbContextOptions<AccountDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === Customer ===
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, guid => CustomerId.From(guid))
                .HasColumnName("id");

            // NationalId → string dönüşümü (DB'de string olarak sakla)
            entity.Property(e => e.NationalId)
                .HasConversion(
                    n => n.Value,
                    s => NationalId.Create(s))
                .HasMaxLength(11)
                .HasColumnName("national_id");
            entity.HasIndex(e => e.NationalId).IsUnique();

            entity.Property(e => e.Email).HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();

            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);

            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            // Customer → Account (1:N)
            entity.HasMany(c => c.Accounts)
                .WithOne()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ignore domain events navigation (EF'e bunları kalıcı olarak sakla deme)
            entity.Ignore(e => e.DomainEvents);
        });

        // === Account ===
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, guid => AccountId.From(guid))
                .HasColumnName("id");

            entity.Property(e => e.CustomerId)
                .HasConversion(id => id.Value, guid => CustomerId.From(guid))
                .HasColumnName("customer_id");

            entity.Property(e => e.AccountNumber).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.AccountNumber).IsUnique();

            entity.Property(e => e.IbanNumber).HasMaxLength(34);

            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Currency).HasConversion<string>().HasMaxLength(3);

            entity.Property(e => e.Balance).HasPrecision(18, 4);

            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
```

- [ ] Tamamlandı

---

## ADIM 3.5 — API DTO'ları

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Api\Dtos\CreateCustomerRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.AccountService.Api.Dtos;

public sealed record CreateCustomerRequest(
    [Required, StringLength(11, MinimumLength = 11)]
    string NationalId,

    [Required, MaxLength(100)]
    string FirstName,

    [Required, MaxLength(100)]
    string LastName,

    [Required, EmailAddress]
    string Email,

    [Required, MaxLength(20)]
    string PhoneNumber,

    [Required]
    DateTime DateOfBirth
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Api\Dtos\OpenAccountRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.AccountService.Api.Dtos;

public sealed record OpenAccountRequest(
    [Required]
    Guid CustomerId,

    [Required]
    string AccountType,   // "Checking", "Savings", "Current"

    [Required]
    string Currency       // "TRY", "USD", "EUR"
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Api\Dtos\CustomerResponse.cs`

```csharp
namespace MiniBank.AccountService.Api.Dtos;

public sealed record CustomerResponse(
    Guid Id,
    string NationalIdMasked,
    string FullName,
    string Email,
    bool KycVerified,
    DateTime CreatedAt
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Api\Dtos\AccountResponse.cs`

```csharp
namespace MiniBank.AccountService.Api.Dtos;

public sealed record AccountResponse(
    Guid Id,
    Guid CustomerId,
    string AccountNumber,
    string Iban,
    string Type,
    string Status,
    string Currency,
    decimal Balance,
    DateTime CreatedAt
);
```

- [ ] Tamamlandı

---

## ADIM 3.6 — Customer Controller

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Api\Controllers\CustomerController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.AccountService.Api.Dtos;
using MiniBank.AccountService.Application;
using MiniBank.AccountService.Domain.Entities;
using MiniBank.AccountService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.AccountService.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize] // JWT zorunlu
public sealed class CustomerController : ControllerBase
{
    private readonly AccountDbContext _db;
    private readonly IKycProvider _kycProvider;

    public CustomerController(AccountDbContext db, IKycProvider kycProvider)
    {
        _db = db;
        _kycProvider = kycProvider;
    }

    /// <summary>
    /// Yeni müşteri kaydı.
    /// 1. TC Kimlik No doğrula (NationalId value object)
    /// 2. KYC doğrulaması yap (stub)
    /// 3. Customer entity oluştur ve kaydet
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        // TC Kimlik No doğrula
        var nationalId = NationalId.Create(request.NationalId);

        // Zaten var mı?
        var exists = await _db.Customers
            .AnyAsync(c => c.NationalId == nationalId, ct);
        if (exists)
            return Conflict(new { error = "CUSTOMER_EXISTS", message = "Customer with this National ID already exists." });

        // KYC doğrulaması
        var kycResult = await _kycProvider.VerifyCustomerAsync(
            request.NationalId, request.FirstName, request.LastName, request.DateOfBirth, ct);

        if (!kycResult.IsVerified)
            return UnprocessableEntity(new { error = "KYC_FAILED", message = kycResult.RejectionReason });

        // Müşteri oluştur
        var customer = Customer.Create(
            nationalId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.DateOfBirth);

        customer.MarkKycVerified();

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/customers/{customer.Id.Value}", new CustomerResponse(
            Id: customer.Id.Value,
            NationalIdMasked: customer.NationalId.Masked,
            FullName: customer.FullName,
            Email: customer.Email,
            KycVerified: customer.KycVerified,
            CreatedAt: customer.CreatedAt
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetCustomer(Guid id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == MiniBank.Shared.Domain.Primitives.CustomerId.From(id), ct);

        if (customer is null)
            return NotFound(new { error = "CUSTOMER_NOT_FOUND" });

        return Ok(new CustomerResponse(
            Id: customer.Id.Value,
            NationalIdMasked: customer.NationalId.Masked,
            FullName: customer.FullName,
            Email: customer.Email,
            KycVerified: customer.KycVerified,
            CreatedAt: customer.CreatedAt
        ));
    }
}
```

- [ ] Tamamlandı

---

## ADIM 3.7 — Account Controller

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\Api\Controllers\AccountController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.AccountService.Api.Dtos;
using MiniBank.AccountService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.AccountService.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly AccountDbContext _db;

    public AccountController(AccountDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> OpenAccount(
        [FromBody] OpenAccountRequest request,
        CancellationToken ct)
    {
        var customer = await _db.Customers
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == CustomerId.From(request.CustomerId), ct);

        if (customer is null)
            return NotFound(new { error = "CUSTOMER_NOT_FOUND" });

        if (!Enum.TryParse<AccountType>(request.AccountType, true, out var accountType))
            return BadRequest(new { error = "INVALID_ACCOUNT_TYPE", valid = Enum.GetNames<AccountType>() });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = "INVALID_CURRENCY", valid = Enum.GetNames<Currency>() });

        var account = customer.OpenAccount(accountType, currency);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/accounts/{account.Id.Value}", ToResponse(account));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountResponse>> GetAccount(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == AccountId.From(id), ct);

        if (account is null)
            return NotFound(new { error = "ACCOUNT_NOT_FOUND" });

        return Ok(ToResponse(account));
    }

    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<ActionResult<List<AccountResponse>>> GetAccountsByCustomer(Guid customerId, CancellationToken ct)
    {
        var accounts = await _db.Accounts
            .Where(a => a.CustomerId == CustomerId.From(customerId))
            .ToListAsync(ct);

        return Ok(accounts.Select(ToResponse).ToList());
    }

    [HttpPut("{id:guid}/freeze")]
    public async Task<IActionResult> FreezeAccount(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == AccountId.From(id), ct);

        if (account is null)
            return NotFound(new { error = "ACCOUNT_NOT_FOUND" });

        account.Freeze();
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Account frozen.", accountId = id });
    }

    [HttpPut("{id:guid}/unfreeze")]
    public async Task<IActionResult> UnfreezeAccount(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == AccountId.From(id), ct);

        if (account is null)
            return NotFound(new { error = "ACCOUNT_NOT_FOUND" });

        account.Unfreeze();
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Account unfrozen.", accountId = id });
    }

    private static AccountResponse ToResponse(MiniBank.AccountService.Domain.Entities.Account a) => new(
        Id: a.Id.Value,
        CustomerId: a.CustomerId.Value,
        AccountNumber: a.AccountNumber,
        Iban: a.IbanNumber,
        Type: a.Type.ToString(),
        Status: a.Status.ToString(),
        Currency: a.Currency.ToString(),
        Balance: a.Balance,
        CreatedAt: a.CreatedAt
    );
}
```

- [ ] Tamamlandı

---

## ADIM 3.8 — Program.cs ve appsettings.json

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AccountService\appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "AccountDb": "Host=localhost;Port=5432;Database=account_db;Username=minibank;Password=minibank_dev_2024"
  },
  "Jwt": {
    "Secret": "MiniBank-Super-Secret-Key-Must-Be-At-Least-32-Characters-Long-2024",
    "Issuer": "MiniBank.IamService",
    "Audience": "MiniBank"
  }
}
```

**DOSYA ÜSTÜNE YAZ:** `src\Services\MiniBank.AccountService\Program.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniBank.AccountService.Application;
using MiniBank.AccountService.Infrastructure.ExternalServices;
using MiniBank.AccountService.Infrastructure.Persistence;
using MiniBank.Shared.Infrastructure.Api;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5002");

// EF Core
builder.Services.AddDbContext<AccountDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AccountDb")));

// KYC Provider (stub)
builder.Services.AddSingleton<IKycProvider, StubKycProvider>();

// JWT
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandler>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "MiniBank.AccountService",
    timestamp = DateTime.UtcNow
}));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

- [ ] Tamamlandı

---

## ADIM 3.9 — Migration ve Test

```powershell
# Migration
dotnet ef migrations add InitialCreate --project src\Services\MiniBank.AccountService --startup-project src\Services\MiniBank.AccountService --output-dir Infrastructure\Persistence\Migrations

# Build
dotnet build MiniBank.sln

# Servisi çalıştır ve test et (IAM service de çalışıyor olmalı)

# 1. IAM'den token al
$loginBody = '{"email":"test@minibank.com","password":"Password123!"}'
$auth = Invoke-RestMethod -Uri http://localhost:5001/api/auth/login -Method POST -Body $loginBody -ContentType "application/json"
$token = $auth.accessToken

# 2. Müşteri oluştur (JWT ile)
$headers = @{ Authorization = "Bearer $token" }
$customerBody = '{"nationalId":"10000000146","firstName":"Ahmet","lastName":"Yilmaz","email":"ahmet@test.com","phoneNumber":"+905551234567","dateOfBirth":"1990-01-15"}'
Invoke-RestMethod -Uri http://localhost:5002/api/customers -Method POST -Body $customerBody -ContentType "application/json" -Headers $headers

# 3. Hesap aç
$accountBody = '{"customerId":"<yukarıdan dönen ID>","accountType":"Checking","currency":"TRY"}'
Invoke-RestMethod -Uri http://localhost:5002/api/accounts -Method POST -Body $accountBody -ContentType "application/json" -Headers $headers
```

- [ ] Tamamlandı

---

## ADIM 3.10 — Git Commit

```powershell
git add -A
git commit -m "Faz 3: Account Service - customer CRUD, account open/freeze, KYC stub"
```

- [ ] Tamamlandı

---

## ✅ FAZ 3 KABUL KRİTERLERİ

```
□ POST /api/customers → müşteri oluşturulur, KYC stub onay verir
□ TC Kimlik No doğrulaması çalışıyor (geçersiz → 400)
□ Aynı TC ile ikinci müşteri → 409 Conflict
□ POST /api/accounts → hesap açılır, IBAN oluşturulur
□ KYC onayı olmadan hesap açma → hata
□ GET /api/accounts/{id} → bakiye bilgisi döner
□ PUT /api/accounts/{id}/freeze → hesap donar
□ Donmuş hesapta bakiye azaltma → AccountFrozenException
□ JWT olmadan istek → 401 Unauthorized
□ Swagger UI çalışıyor (http://localhost:5002/swagger)
□ dotnet build → 0 error
□ Git commit yapıldı
```

**Faz 3 tamamlandıysa, bu satırı işaretle:** ⬜ → ✅

---

*Faz 4 (Card Service) ayrı dosyada: `IMPLEMENTATION_FAZ4.md`*
