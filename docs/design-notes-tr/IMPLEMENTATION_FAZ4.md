# FAZ 4 — CARD SERVICE (Kart Yaşam Döngüsü & Limit Yönetimi)

> **ÖN KOŞUL:** Faz 0-3 tamamlanmış. Customer ve Account oluşturulabiliyor.
> **PORT:** 5003
> **VERİTABANI:** card_db

**AMAÇ:** Kart oluşturma, aktivasyon, bloke, limit tanımlama/güncelleme. Redis'te limit cache'leme.

**BU FAZIN SONUNDA:**
- `POST /api/cards` → yeni kart üretimi (PAN, CVV, expiry)
- `PUT /api/cards/{id}/activate` → kart aktivasyonu
- `PUT /api/cards/{id}/block` → kart bloke
- `POST /api/cards/{id}/limits` → limit tanımlama
- `GET /api/cards/{id}` → kart bilgisi (PAN maskelenmiş)
- Limitler Redis'te cache'lenmiş, atomik kontrol edilebilir

---

## ADIM 4.0 — Klasör Yapısı ve NuGet Paketleri

```powershell
mkdir src\Services\MiniBank.CardService\Domain
mkdir src\Services\MiniBank.CardService\Domain\Entities
mkdir src\Services\MiniBank.CardService\Domain\Events
mkdir src\Services\MiniBank.CardService\Infrastructure
mkdir src\Services\MiniBank.CardService\Infrastructure\Persistence
mkdir src\Services\MiniBank.CardService\Application
mkdir src\Services\MiniBank.CardService\Api
mkdir src\Services\MiniBank.CardService\Api\Controllers
mkdir src\Services\MiniBank.CardService\Api\Dtos

dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package Microsoft.EntityFrameworkCore
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package System.IdentityModel.Tokens.Jwt
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package Microsoft.IdentityModel.Tokens
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package StackExchange.Redis
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package Swashbuckle.AspNetCore
dotnet add src\Services\MiniBank.CardService\MiniBank.CardService.csproj package Serilog.AspNetCore
```

- [ ] Tamamlandı

---

## ADIM 4.1 — Card Entity

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\Domain\Entities\Card.cs`

```csharp
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.CardService.Domain.Entities;

/// <summary>
/// Kart entity. Her kartın:
/// - PAN'ı var (hash olarak DB'de, mask olarak API'de)
/// - CVV'si var (hash olarak DB'de, ASLA API'de gösterilmez)
/// - Expiry tarihi var
/// - Status'ü var (Created → Active → Blocked/Expired/Closed)
/// - Bir AccountId'ye bağlı
/// - Limitleri var
///
/// State machine geçişleri:
///   Created → Active (aktivasyon)
///   Active → Blocked (kullanıcı talebi veya fraud)
///   Active → StolenLost (kayıp/çalıntı)
///   Blocked → Active (bloke kaldırma)
///   Active → Expired (tarih doldu)
///   * → Closed (kesin kapatma)
/// </summary>
public sealed class Card : AggregateRoot<CardId>, IAggregateWithEvents
{
    public AccountId AccountId { get; private set; } = default!;
    public CustomerId CustomerId { get; private set; } = default!;
    public string PanHash { get; private set; } = string.Empty;
    public string PanLast4 { get; private set; } = string.Empty;
    public string PanMasked { get; private set; } = string.Empty;
    public string CvvHash { get; private set; } = string.Empty;
    public CardExpiry Expiry { get; private set; } = default!;
    public CardType Type { get; private set; }
    public CardStatus Status { get; private set; }
    public string CardholderName { get; private set; } = string.Empty;

    private readonly List<CardLimit> _limits = [];
    public IReadOnlyCollection<CardLimit> Limits => _limits.AsReadOnly();

    private Card() { }

    public static Card Create(
        AccountId accountId,
        CustomerId customerId,
        Pan pan,
        string cvvHash,
        CardExpiry expiry,
        CardType type,
        string cardholderName)
    {
        var card = new Card
        {
            Id = CardId.New(),
            AccountId = accountId,
            CustomerId = customerId,
            PanHash = pan.Hashed,
            PanLast4 = pan.Last4,
            PanMasked = pan.Masked,
            CvvHash = cvvHash,
            Expiry = expiry,
            Type = type,
            Status = CardStatus.Created,
            CardholderName = cardholderName.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };

        return card;
    }

    public void Activate()
    {
        EnsureTransition(CardStatus.Created, CardStatus.Active);
        Status = CardStatus.Active;
        MarkModified();
    }

    public void Block()
    {
        EnsureTransition(CardStatus.Active, CardStatus.Blocked);
        Status = CardStatus.Blocked;
        MarkModified();
    }

    public void Unblock()
    {
        EnsureTransition(CardStatus.Blocked, CardStatus.Active);
        Status = CardStatus.Active;
        MarkModified();
    }

    public void ReportStolenLost()
    {
        if (Status is CardStatus.Closed or CardStatus.StolenLost)
            throw new InvalidStateTransitionException("Card", Status.ToString(), CardStatus.StolenLost.ToString());

        Status = CardStatus.StolenLost;
        MarkModified();
    }

    public void Close()
    {
        if (Status == CardStatus.Closed)
            throw new InvalidStateTransitionException("Card", Status.ToString(), CardStatus.Closed.ToString());

        Status = CardStatus.Closed;
        MarkModified();
    }

    /// <summary>
    /// İşlem için kartın kullanılabilir olduğunu doğrular.
    /// Authorization service bu metodu çağırır.
    /// </summary>
    public void EnsureCanTransact(DateTime utcNow)
    {
        if (Status != CardStatus.Active)
            throw new CardNotActiveException(Id.Value.ToString());

        if (Expiry.IsExpired(utcNow))
            throw new DomainExceptionImpl("CARD_EXPIRED", "Card has expired.");
    }

    public CardLimit SetLimit(LimitType limitType, decimal amount, Currency currency)
    {
        var existing = _limits.FirstOrDefault(l => l.LimitType == limitType);
        if (existing is not null)
        {
            existing.UpdateAmount(amount);
            MarkModified();
            return existing;
        }

        var limit = CardLimit.Create(Id, limitType, amount, currency);
        _limits.Add(limit);
        MarkModified();
        return limit;
    }

    private void EnsureTransition(CardStatus expectedFrom, CardStatus to)
    {
        if (Status != expectedFrom)
            throw new InvalidStateTransitionException("Card", Status.ToString(), to.ToString());
    }

    IReadOnlyCollection<object> IAggregateWithEvents.GetDomainEvents()
        => DomainEvents.Cast<object>().ToList().AsReadOnly();
    void IAggregateWithEvents.ClearEvents() => ClearDomainEvents();
}

/// <summary>
/// Genel amaçlı DomainException (code + message).
/// Specific exception class oluşturmaya gerek olmayan durumlar için.
/// </summary>
internal sealed class DomainExceptionImpl : DomainException
{
    public DomainExceptionImpl(string code, string message) : base(code, message) { }
}
```

- [ ] Tamamlandı

---

## ADIM 4.2 — CardLimit Entity

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\Domain\Entities\CardLimit.cs`

```csharp
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.CardService.Domain.Entities;

/// <summary>
/// Kart limiti. Her kart için farklı limit türleri tanımlanabilir.
/// Limitler Redis'te de cache'lenir (real-time kontrol için).
/// DB → source of truth, Redis → real-time check + atomic decrement.
/// </summary>
public sealed class CardLimit
{
    public Guid Id { get; private set; }
    public CardId CardId { get; private set; } = default!;
    public LimitType LimitType { get; private set; }
    public decimal MaxAmount { get; private set; }
    public decimal UsedAmount { get; private set; }
    public Currency Currency { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CardLimit() { }

    internal static CardLimit Create(CardId cardId, LimitType limitType, decimal maxAmount, Currency currency)
    {
        return new CardLimit
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            LimitType = limitType,
            MaxAmount = maxAmount,
            UsedAmount = 0,
            Currency = currency,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateAmount(decimal newMaxAmount)
    {
        if (newMaxAmount <= 0)
            throw new ArgumentException("Limit amount must be positive.", nameof(newMaxAmount));

        MaxAmount = newMaxAmount;
        UpdatedAt = DateTime.UtcNow;
    }

    public decimal RemainingAmount => MaxAmount - UsedAmount;

    /// <summary>
    /// Redis cache key formatı: "limit:{cardId}:{limitType}"
    /// Authorization service bu key ile Redis'ten limit kontrolü yapar.
    /// </summary>
    public string CacheKey => $"limit:{CardId.Value}:{LimitType}";
}
```

- [ ] Tamamlandı

---

## ADIM 4.3 — Card DbContext

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\Infrastructure\Persistence\CardDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using MiniBank.CardService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.CardService.Infrastructure.Persistence;

public sealed class CardDbContext : BaseDbContext
{
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardLimit> CardLimits => Set<CardLimit>();

    public CardDbContext(DbContextOptions<CardDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Card>(entity =>
        {
            entity.ToTable("cards");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, guid => CardId.From(guid))
                .HasColumnName("id");

            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, guid => AccountId.From(guid))
                .HasColumnName("account_id");

            entity.Property(e => e.CustomerId)
                .HasConversion(id => id.Value, guid => CustomerId.From(guid))
                .HasColumnName("customer_id");

            entity.Property(e => e.PanHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.PanHash).IsUnique();

            entity.Property(e => e.PanLast4).HasMaxLength(4).IsRequired();
            entity.Property(e => e.PanMasked).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CvvHash).HasMaxLength(64).IsRequired();

            entity.Property(e => e.Expiry)
                .HasConversion(
                    e => e.ToString(),
                    s => CardExpiry.Parse(s))
                .HasMaxLength(5)
                .HasColumnName("expiry");

            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.CardholderName).HasMaxLength(50);
            entity.Property(e => e.Version).IsConcurrencyToken();

            entity.HasMany(c => c.Limits)
                .WithOne()
                .HasForeignKey(l => l.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Ignore(e => e.DomainEvents);
        });

        modelBuilder.Entity<CardLimit>(entity =>
        {
            entity.ToTable("card_limits");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CardId)
                .HasConversion(id => id.Value, guid => CardId.From(guid))
                .HasColumnName("card_id");

            entity.Property(e => e.LimitType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Currency).HasConversion<string>().HasMaxLength(3);
            entity.Property(e => e.MaxAmount).HasPrecision(18, 4);
            entity.Property(e => e.UsedAmount).HasPrecision(18, 4);

            entity.HasIndex(e => new { e.CardId, e.LimitType }).IsUnique();
        });
    }
}
```

- [ ] Tamamlandı

---

## ADIM 4.4 — Card Controller ve DTO'lar

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\Api\Dtos\CreateCardRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.CardService.Api.Dtos;

public sealed record CreateCardRequest(
    [Required] Guid AccountId,
    [Required] Guid CustomerId,
    [Required] string CardType,        // "Debit", "Credit"
    [Required, MaxLength(50)] string CardholderName
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\Api\Dtos\SetLimitRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.CardService.Api.Dtos;

public sealed record SetLimitRequest(
    [Required] string LimitType,       // "SingleTransaction", "Daily", "Monthly"
    [Required, Range(1, 999999999)] decimal MaxAmount,
    [Required] string Currency          // "TRY", "USD"
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\Api\Dtos\CardResponse.cs`

```csharp
namespace MiniBank.CardService.Api.Dtos;

public sealed record CardResponse(
    Guid Id,
    Guid AccountId,
    string PanMasked,      // "453201******0366"
    string Last4,          // "0366"
    string Expiry,         // "12/27"
    string Type,
    string Status,
    string CardholderName,
    List<LimitResponse> Limits
);

public sealed record LimitResponse(
    string LimitType,
    decimal MaxAmount,
    decimal UsedAmount,
    decimal RemainingAmount,
    string Currency
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\Api\Controllers\CardController.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.CardService.Api.Dtos;
using MiniBank.CardService.Domain.Entities;
using MiniBank.CardService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.Shared.Infrastructure.Caching;

namespace MiniBank.CardService.Api.Controllers;

[ApiController]
[Route("api/cards")]
[Authorize]
public sealed class CardController : ControllerBase
{
    private readonly CardDbContext _db;
    private readonly ICacheService _cache;

    public CardController(CardDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// Yeni kart üretir.
    /// 1. PAN oluşturur (test: random 16 hane + Luhn geçerli)
    /// 2. CVV oluşturur (3 hane random)
    /// 3. Expiry: bugünden 5 yıl sonra
    /// 4. PAN ve CVV hash'lenerek DB'ye yazılır
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CardResponse>> CreateCard(
        [FromBody] CreateCardRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CardType>(request.CardType, true, out var cardType))
            return BadRequest(new { error = "INVALID_CARD_TYPE" });

        // PAN oluştur (test amaçlı random — production'da kartlı sistem üretir)
        var panNumber = GenerateTestPan();
        var pan = Pan.Create(panNumber);

        // CVV oluştur ve hashle
        var cvv = Random.Shared.Next(100, 999).ToString();
        var cvvHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(cvv)));

        // Expiry: bugünden 5 yıl sonra
        var expiry = CardExpiry.Create(DateTime.UtcNow.Month, DateTime.UtcNow.Year + 5);

        var card = Card.Create(
            AccountId.From(request.AccountId),
            CustomerId.From(request.CustomerId),
            pan,
            cvvHash,
            expiry,
            cardType,
            request.CardholderName);

        _db.Cards.Add(card);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/cards/{card.Id.Value}", ToResponse(card));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CardResponse>> GetCard(Guid id, CancellationToken ct)
    {
        var card = await _db.Cards
            .Include(c => c.Limits)
            .FirstOrDefaultAsync(c => c.Id == CardId.From(id), ct);

        if (card is null)
            return NotFound(new { error = "CARD_NOT_FOUND" });

        return Ok(ToResponse(card));
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var card = await FindCard(id, ct);
        if (card is null) return NotFound(new { error = "CARD_NOT_FOUND" });

        card.Activate();
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Card activated.", cardId = id });
    }

    [HttpPut("{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, CancellationToken ct)
    {
        var card = await FindCard(id, ct);
        if (card is null) return NotFound(new { error = "CARD_NOT_FOUND" });

        card.Block();
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Card blocked.", cardId = id });
    }

    [HttpPut("{id:guid}/unblock")]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        var card = await FindCard(id, ct);
        if (card is null) return NotFound(new { error = "CARD_NOT_FOUND" });

        card.Unblock();
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Card unblocked.", cardId = id });
    }

    /// <summary>
    /// Limit tanımla veya güncelle.
    /// 1. DB'ye yaz
    /// 2. Redis'e cache'le (Authorization service buradan okuyacak)
    /// </summary>
    [HttpPost("{id:guid}/limits")]
    public async Task<ActionResult<LimitResponse>> SetLimit(
        Guid id,
        [FromBody] SetLimitRequest request,
        CancellationToken ct)
    {
        var card = await _db.Cards
            .Include(c => c.Limits)
            .FirstOrDefaultAsync(c => c.Id == CardId.From(id), ct);

        if (card is null)
            return NotFound(new { error = "CARD_NOT_FOUND" });

        if (!Enum.TryParse<LimitType>(request.LimitType, true, out var limitType))
            return BadRequest(new { error = "INVALID_LIMIT_TYPE" });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = "INVALID_CURRENCY" });

        var limit = card.SetLimit(limitType, request.MaxAmount, currency);
        await _db.SaveChangesAsync(ct);

        // Redis'e cache'le — kalan limiti integer olarak
        var cacheKey = limit.CacheKey;
        var remainingMinor = (long)(limit.RemainingAmount * 100); // kuruş cinsinden
        await _cache.SetAsync(cacheKey,
            new CachedLimit(limit.MaxAmount, limit.RemainingAmount),
            TimeSpan.FromHours(24));

        return Ok(new LimitResponse(
            limit.LimitType.ToString(),
            limit.MaxAmount,
            limit.UsedAmount,
            limit.RemainingAmount,
            limit.Currency.ToString()
        ));
    }

    private async Task<Card?> FindCard(Guid id, CancellationToken ct)
        => await _db.Cards.FirstOrDefaultAsync(c => c.Id == CardId.From(id), ct);

    /// <summary>
    /// Luhn-geçerli test PAN üretir. Production'da HSM üretir.
    /// </summary>
    private static string GenerateTestPan()
    {
        var prefix = "453201"; // test BIN
        var body = "";
        for (var i = 0; i < 9; i++)
            body += Random.Shared.Next(0, 10);

        var partial = prefix + body;
        var checkDigit = CalculateLuhnCheckDigit(partial);
        return partial + checkDigit;
    }

    private static string CalculateLuhnCheckDigit(string number)
    {
        var sum = 0;
        var alternate = true;
        for (var i = number.Length - 1; i >= 0; i--)
        {
            var digit = number[i] - '0';
            if (alternate)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
            alternate = !alternate;
        }
        return ((10 - (sum % 10)) % 10).ToString();
    }

    private static CardResponse ToResponse(Card c) => new(
        Id: c.Id.Value,
        AccountId: c.AccountId.Value,
        PanMasked: c.PanMasked,
        Last4: c.PanLast4,
        Expiry: c.Expiry.ToString(),
        Type: c.Type.ToString(),
        Status: c.Status.ToString(),
        CardholderName: c.CardholderName,
        Limits: c.Limits.Select(l => new LimitResponse(
            l.LimitType.ToString(), l.MaxAmount, l.UsedAmount, l.RemainingAmount, l.Currency.ToString()
        )).ToList()
    );
}

internal sealed record CachedLimit(decimal MaxAmount, decimal RemainingAmount);
```

- [ ] Tamamlandı

---

## ADIM 4.5 — Program.cs ve appsettings.json

**DOSYA OLUŞTUR:** `src\Services\MiniBank.CardService\appsettings.json`

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "CardDb": "Host=localhost;Port=5432;Database=card_db;Username=minibank;Password=minibank_dev_2024",
    "Redis": "localhost:6379,password=redis_dev_2024"
  },
  "Jwt": {
    "Secret": "MiniBank-Super-Secret-Key-Must-Be-At-Least-32-Characters-Long-2024",
    "Issuer": "MiniBank.IamService",
    "Audience": "MiniBank"
  }
}
```

**DOSYA ÜSTÜNE YAZ:** `src\Services\MiniBank.CardService\Program.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniBank.CardService.Infrastructure.Persistence;
using MiniBank.Shared.Infrastructure.Api;
using MiniBank.Shared.Infrastructure.Caching;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5003");

// EF Core
builder.Services.AddDbContext<CardDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CardDb")));

// Redis
var redisConnection = builder.Configuration.GetConnectionString("Redis")!;
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

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
    service = "MiniBank.CardService",
    timestamp = DateTime.UtcNow
}));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CardDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

- [ ] Tamamlandı

---

## ADIM 4.6 — Migration, Build, Test

```powershell
dotnet ef migrations add InitialCreate --project src\Services\MiniBank.CardService --startup-project src\Services\MiniBank.CardService --output-dir Infrastructure\Persistence\Migrations

dotnet build MiniBank.sln

# Testi çalıştır — IAM ve Account service'ler çalışıyor olmalı
# 1. Token al, 2. Müşteri oluştur, 3. Hesap aç, 4. Kart oluştur
```

- [ ] Tamamlandı

---

## ADIM 4.7 — Git Commit

```powershell
git add -A
git commit -m "Faz 4: Card Service - kart CRUD, state machine, limit yönetimi, Redis cache"
```

- [ ] Tamamlandı

---

## ✅ FAZ 4 KABUL KRİTERLERİ

```
□ POST /api/cards → kart oluşturulur, PAN Luhn-geçerli
□ GET /api/cards/{id} → PAN maskelenmiş döner, CVV ASLA dönmez
□ PUT /api/cards/{id}/activate → Created→Active geçişi başarılı
□ PUT /api/cards/{id}/block → Active→Blocked geçişi başarılı
□ Geçersiz state transition → InvalidStateTransitionException
□ POST /api/cards/{id}/limits → limit oluşturulur, Redis'e cache'lenir
□ Redis'te limit key var: "limit:{cardId}:{limitType}"
□ Card entity → Pan.Create ile oluşturuluyor, hash ve mask doğru
□ CardExpiry → IsExpired doğru çalışıyor
□ dotnet build → 0 error
□ Git commit yapıldı
```

**Faz 4 tamamlandıysa, bu satırı işaretle:** ⬜ → ✅

---

*Faz 5 (Authorization Service) ayrı dosyada: `IMPLEMENTATION_FAZ5.md`*
