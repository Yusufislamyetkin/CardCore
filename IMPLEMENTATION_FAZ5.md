# FAZ 5 — AUTHORIZATION SERVICE (Ödeme Onay & Provizyon)

> **ÖN KOŞUL:** Faz 0-4 tamamlanmış. Kart ve limit oluşturulabiliyor.
> **PORT:** 5004
> **VERİTABANI:** authorization_db

**AMAÇ:** Ödeme isteğini alır, kart durumunu kontrol eder, limitleri Redis'ten atomik düşer, ISO 8583 benzeri response code döner. Bu bankacılığın en kritik servisi — her POS işlemi buradan geçer.

**BU FAZIN SONUNDA:**
- `POST /api/authorizations` → ödeme onay isteği (provizyon)
- `POST /api/authorizations/{id}/reverse` → provizyon iptali (void)
- Kart kontrolü: aktif mi, süresi dolmamış mı
- Limit kontrolü: Redis'ten atomik kontrol
- Fraud kontrolü: Fraud service'e istek (stub)
- ISO 8583 benzeri response code'lar (00=onay, 51=yetersiz bakiye, vb.)
- Onaylanan işlem Kafka'ya event olarak gönderilir

---

## ADIM 5.0 — Klasör Yapısı ve NuGet Paketleri

```powershell
mkdir src\Services\MiniBank.AuthorizationService\Domain
mkdir src\Services\MiniBank.AuthorizationService\Domain\Entities
mkdir src\Services\MiniBank.AuthorizationService\Infrastructure
mkdir src\Services\MiniBank.AuthorizationService\Infrastructure\Persistence
mkdir src\Services\MiniBank.AuthorizationService\Application
mkdir src\Services\MiniBank.AuthorizationService\Api
mkdir src\Services\MiniBank.AuthorizationService\Api\Controllers
mkdir src\Services\MiniBank.AuthorizationService\Api\Dtos

dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package Microsoft.EntityFrameworkCore
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package System.IdentityModel.Tokens.Jwt
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package Microsoft.IdentityModel.Tokens
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package StackExchange.Redis
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package Swashbuckle.AspNetCore
dotnet add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj package Serilog.AspNetCore
```

- [ ] Tamamlandı

---

## ADIM 5.1 — Authorization Entity

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AuthorizationService\Domain\Entities\AuthorizationRecord.cs`

```csharp
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.AuthorizationService.Domain.Entities;

/// <summary>
/// Provizyon kaydı. POS'tan gelen her işlem isteği bir authorization kaydı oluşturur.
/// 
/// Flow:
/// 1. POS → Authorization Service: "4532****0366 ile 150 TRY satın alma"
/// 2. Kart kontrolü (aktif mi, süresi dolmamış mı)
/// 3. Limit kontrolü (Redis'ten atomik decrement)
/// 4. Fraud kontrolü (velocity check)
/// 5. Onay → RC=00, AuthCode üretilir
/// 6. Red → RC=51 (yetersiz limit), RC=14 (kart aktif değil), vb.
/// 
/// ISO 8583 Field karşılıkları:
/// - Field 2 (PAN): PanHash
/// - Field 3 (Processing Code): TransactionType
/// - Field 4 (Transaction Amount): Amount/Currency
/// - Field 38 (Auth Code): AuthorizationCode
/// - Field 39 (Response Code): ResponseCode
/// </summary>
public sealed class AuthorizationRecord : Entity<AuthorizationId>
{
    public CardId CardId { get; private set; } = default!;
    public AccountId AccountId { get; private set; } = default!;
    public string PanHash { get; private set; } = string.Empty;
    public TransactionType TransactionType { get; private set; }
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }
    public string MerchantId { get; private set; } = string.Empty;
    public string MerchantName { get; private set; } = string.Empty;
    public string? AuthorizationCode { get; private set; }
    public string ResponseCode { get; private set; } = string.Empty;
    public DeclineReason DeclineReason { get; private set; }
    public TransactionStatus Status { get; private set; }
    public bool IsReversed { get; private set; }
    public DateTime? ReversedAt { get; private set; }

    private AuthorizationRecord() { }

    public static AuthorizationRecord Create(
        CardId cardId,
        AccountId accountId,
        string panHash,
        TransactionType transactionType,
        decimal amount,
        Currency currency,
        string merchantId,
        string merchantName)
    {
        return new AuthorizationRecord
        {
            Id = AuthorizationId.New(),
            CardId = cardId,
            AccountId = accountId,
            PanHash = panHash,
            TransactionType = transactionType,
            Amount = amount,
            Currency = currency,
            MerchantId = merchantId,
            MerchantName = merchantName,
            Status = TransactionStatus.Pending,
            DeclineReason = DeclineReason.None,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };
    }

    /// <summary>
    /// İşlemi onayla. Authorization code üretilir.
    /// Authorization code: 6 haneli alfanumerik (ISO 8583 Field 38).
    /// </summary>
    public void Approve()
    {
        AuthorizationCode = GenerateAuthCode();
        ResponseCode = "00"; // ISO 8583: Approved
        Status = TransactionStatus.Approved;
        DeclineReason = DeclineReason.None;
        MarkModified();
    }

    /// <summary>
    /// İşlemi reddet.
    /// </summary>
    public void Decline(DeclineReason reason)
    {
        ResponseCode = reason switch
        {
            DeclineReason.InsufficientFunds => "51",
            DeclineReason.CardNotActive => "14",
            DeclineReason.CardExpired => "54",
            DeclineReason.LimitExceeded => "61",
            DeclineReason.FraudSuspected => "59",
            DeclineReason.InvalidMerchant => "03",
            DeclineReason.InvalidPin => "55",
            DeclineReason.SystemError => "96",
            _ => "05" // Do not honor
        };

        Status = TransactionStatus.Declined;
        DeclineReason = reason;
        MarkModified();
    }

    /// <summary>
    /// Provizyon iptali (void).
    /// Onaylanmış işlemi geri alır. Limit Redis'e geri eklenir.
    /// </summary>
    public void Reverse()
    {
        if (Status != TransactionStatus.Approved)
            throw new InvalidOperationException("Only approved authorizations can be reversed.");

        IsReversed = true;
        ReversedAt = DateTime.UtcNow;
        Status = TransactionStatus.Reversed;
        MarkModified();
    }

    private static string GenerateAuthCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var code = new char[6];
        for (var i = 0; i < 6; i++)
            code[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(code);
    }
}
```

- [ ] Tamamlandı

---

## ADIM 5.2 — Authorization DbContext

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AuthorizationService\Infrastructure\Persistence\AuthorizationDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using MiniBank.AuthorizationService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.AuthorizationService.Infrastructure.Persistence;

public sealed class AuthorizationDbContext : BaseDbContext
{
    public DbSet<AuthorizationRecord> Authorizations => Set<AuthorizationRecord>();

    public AuthorizationDbContext(DbContextOptions<AuthorizationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuthorizationRecord>(entity =>
        {
            entity.ToTable("authorizations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, guid => AuthorizationId.From(guid))
                .HasColumnName("id");

            entity.Property(e => e.CardId)
                .HasConversion(id => id.Value, guid => CardId.From(guid))
                .HasColumnName("card_id");

            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, guid => AccountId.From(guid))
                .HasColumnName("account_id");

            entity.Property(e => e.PanHash).HasMaxLength(64);
            entity.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasConversion<string>().HasMaxLength(3);
            entity.Property(e => e.MerchantId).HasMaxLength(50);
            entity.Property(e => e.MerchantName).HasMaxLength(200);
            entity.Property(e => e.AuthorizationCode).HasMaxLength(6);
            entity.Property(e => e.ResponseCode).HasMaxLength(2);
            entity.Property(e => e.DeclineReason).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Version).IsConcurrencyToken();

            entity.HasIndex(e => e.CardId);
            entity.HasIndex(e => new { e.CardId, e.CreatedAt });
        });
    }
}
```

- [ ] Tamamlandı

---

## ADIM 5.3 — Authorization DTO'ları

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AuthorizationService\Api\Dtos\AuthorizationRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.AuthorizationService.Api.Dtos;

/// <summary>
/// POS'tan gelen ödeme onay isteği.
/// Gerçekte ISO 8583 mesajı olarak gelir — biz JSON API olarak simüle ediyoruz.
/// </summary>
public sealed record AuthorizationRequest(
    [Required] Guid CardId,
    [Required] Guid AccountId,
    [Required] string TransactionType,  // "Purchase", "CashAdvance", "Refund"
    [Required, Range(0.01, 999999999)] decimal Amount,
    [Required] string Currency,         // "TRY", "USD"
    [Required, MaxLength(50)] string MerchantId,
    [Required, MaxLength(200)] string MerchantName
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AuthorizationService\Api\Dtos\AuthorizationResponse.cs`

```csharp
namespace MiniBank.AuthorizationService.Api.Dtos;

/// <summary>
/// ISO 8583 benzeri response.
/// </summary>
public sealed record AuthorizationResponse(
    Guid AuthorizationId,
    string ResponseCode,       // "00" = onay, "51" = yetersiz bakiye, vb.
    string ResponseMessage,    // insan-okunabilir açıklama
    string? AuthorizationCode, // 6 haneli auth code (onaylıysa)
    string Status,             // "Approved", "Declined"
    decimal Amount,
    string Currency
);
```

- [ ] Tamamlandı

---

## ADIM 5.4 — Authorization Controller

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AuthorizationService\Api\Controllers\AuthorizationController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.AuthorizationService.Api.Dtos;
using MiniBank.AuthorizationService.Domain.Entities;
using MiniBank.AuthorizationService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Caching;

namespace MiniBank.AuthorizationService.Api.Controllers;

[ApiController]
[Route("api/authorizations")]
[Authorize]
public sealed class AuthorizationController : ControllerBase
{
    private readonly AuthorizationDbContext _db;
    private readonly ICacheService _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        AuthorizationDbContext db,
        ICacheService cache,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthorizationController> logger)
    {
        _db = db;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Ödeme onay isteği (provizyon).
    /// 
    /// AKIŞ:
    /// 1. Gelen parametreleri doğrula (transaction type, currency)
    /// 2. Kart kontrolü: Card Service'ten kartı oku → aktif mi, süresi dolmuş mu
    /// 3. Limit kontrolü: Redis'ten atomik kontrol
    /// 4. Her şey OK → ONAY (RC=00), değilse → RED (RC=51/14/54/61)
    /// 5. Authorization kaydını DB'ye yaz
    /// 6. Onaylandıysa: Kafka event gönder (outbox ile)
    ///
    /// Bu versiyonda Card Service'e HTTP çağrı yapmak yerine,
    /// basitlik için limit kontrolü doğrudan Redis'ten yapılır.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AuthorizationResponse>> Authorize(
        [FromBody] AuthorizationRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Authorization request: CardId={CardId}, Amount={Amount} {Currency}, Merchant={Merchant}",
            request.CardId, request.Amount, request.Currency, request.MerchantName);

        // 1. Parametreleri doğrula
        if (!Enum.TryParse<TransactionType>(request.TransactionType, true, out var txnType))
            return BadRequest(new { error = "INVALID_TRANSACTION_TYPE" });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = "INVALID_CURRENCY" });

        // Authorization kaydı oluştur
        var authRecord = AuthorizationRecord.Create(
            CardId.From(request.CardId),
            AccountId.From(request.AccountId),
            "", // PAN hash — Card Service'ten alınacak
            txnType,
            request.Amount,
            currency,
            request.MerchantId,
            request.MerchantName);

        // 2. Limit kontrolü (Redis)
        var limitKey = $"limit:{request.CardId}:Daily"; // günlük limit
        var remainingResult = await _cache.DecrementIfSufficientAsync(
            limitKey, (long)(request.Amount * 100)); // kuruş cinsinden

        if (remainingResult < 0)
        {
            // Limit yetersiz
            authRecord.Decline(DeclineReason.LimitExceeded);
            _db.Authorizations.Add(authRecord);
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning("Authorization DECLINED: limit exceeded. CardId={CardId}", request.CardId);

            return Ok(ToResponse(authRecord, "Daily limit exceeded."));
        }

        // 3. ONAY
        authRecord.Approve();
        _db.Authorizations.Add(authRecord);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Authorization APPROVED: AuthCode={AuthCode}, CardId={CardId}, Amount={Amount}",
            authRecord.AuthorizationCode, request.CardId, request.Amount);

        return Ok(ToResponse(authRecord, "Approved."));
    }

    /// <summary>
    /// Provizyon iptali (void). Onaylanmış işlemi geri alır.
    /// Redis'teki limitler geri yüklenir.
    /// </summary>
    [HttpPost("{id:guid}/reverse")]
    public async Task<ActionResult<AuthorizationResponse>> Reverse(Guid id, CancellationToken ct)
    {
        var auth = await _db.Authorizations
            .FirstOrDefaultAsync(a => a.Id == AuthorizationId.From(id), ct);

        if (auth is null)
            return NotFound(new { error = "AUTHORIZATION_NOT_FOUND" });

        auth.Reverse();
        await _db.SaveChangesAsync(ct);

        // Redis'te limiti geri ekle (TODO: INCRBY ile — şu anda basitleştirilmiş)
        _logger.LogInformation("Authorization REVERSED: {Id}", id);

        return Ok(ToResponse(auth, "Authorization reversed."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuthorizationResponse>> GetAuthorization(Guid id, CancellationToken ct)
    {
        var auth = await _db.Authorizations
            .FirstOrDefaultAsync(a => a.Id == AuthorizationId.From(id), ct);

        if (auth is null)
            return NotFound(new { error = "AUTHORIZATION_NOT_FOUND" });

        return Ok(ToResponse(auth, ""));
    }

    private static AuthorizationResponse ToResponse(AuthorizationRecord a, string message) => new(
        AuthorizationId: a.Id.Value,
        ResponseCode: a.ResponseCode,
        ResponseMessage: message,
        AuthorizationCode: a.AuthorizationCode,
        Status: a.Status.ToString(),
        Amount: a.Amount,
        Currency: a.Currency.ToString()
    );
}
```

- [ ] Tamamlandı

---

## ADIM 5.5 — Program.cs ve appsettings.json

**DOSYA OLUŞTUR:** `src\Services\MiniBank.AuthorizationService\appsettings.json`

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "AuthorizationDb": "Host=localhost;Port=5432;Database=authorization_db;Username=minibank;Password=minibank_dev_2024",
    "Redis": "localhost:6379,password=redis_dev_2024"
  },
  "Jwt": {
    "Secret": "MiniBank-Super-Secret-Key-Must-Be-At-Least-32-Characters-Long-2024",
    "Issuer": "MiniBank.IamService",
    "Audience": "MiniBank"
  }
}
```

**DOSYA ÜSTÜNE YAZ:** `src\Services\MiniBank.AuthorizationService\Program.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniBank.AuthorizationService.Infrastructure.Persistence;
using MiniBank.Shared.Infrastructure.Api;
using MiniBank.Shared.Infrastructure.Caching;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5004");

builder.Services.AddDbContext<AuthorizationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthorizationDb")));

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

builder.Services.AddHttpClient();

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
    service = "MiniBank.AuthorizationService",
    timestamp = DateTime.UtcNow
}));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

- [ ] Tamamlandı

---

## ADIM 5.6 — Migration, Build, Test

```powershell
dotnet ef migrations add InitialCreate --project src\Services\MiniBank.AuthorizationService --startup-project src\Services\MiniBank.AuthorizationService --output-dir Infrastructure\Persistence\Migrations

dotnet build MiniBank.sln

# E2E Test akışı:
# 1. IAM → login → token al
# 2. Account → müşteri + hesap aç
# 3. Card → kart oluştur + aktive et + limit tanımla
# 4. Authorization → ödeme isteği gönder
# Beklenen: RC=00 (onay) veya RC=61 (limit aşıldı)
```

- [ ] Tamamlandı

---

## ADIM 5.7 — Git Commit

```powershell
git add -A
git commit -m "Faz 5: Authorization Service - provizyon onay/red, ISO 8583 RC, Redis limit kontrolu"
```

- [ ] Tamamlandı

---

## ✅ FAZ 5 KABUL KRİTERLERİ

```
□ POST /api/authorizations → limit yeterliyse RC=00, AuthCode döner
□ POST /api/authorizations → limit yetersizse RC=61, Declined
□ POST /api/authorizations/{id}/reverse → onaylı işlem iptal edilir
□ Onaysız işlemi iptal etme → hata
□ Redis DecrementIfSufficientAsync atomik çalışıyor
□ Authorization kaydı DB'de — CardId, Amount, RC, AuthCode
□ ISO 8583 RC mapping doğru (00, 14, 51, 54, 61)
□ dotnet build → 0 error
□ Git commit yapıldı
```

**Faz 5 tamamlandıysa, bu satırı işaretle:** ⬜ → ✅

---

*Faz 6 (Transaction/Ledger Service — çift taraflı muhasebe) ayrı dosyada: `IMPLEMENTATION_FAZ6.md`*
*Faz 7 (Installment Service — taksit hesaplama) ayrı dosyada: `IMPLEMENTATION_FAZ7.md`*
*Faz 8 (Fraud Service — dolandırıcılık tespit) ayrı dosyada: `IMPLEMENTATION_FAZ8.md`*
*Faz 9 (EOD Service — gün sonu batch) ayrı dosyada: `IMPLEMENTATION_FAZ9.md`*
*Faz 10-15 ayrı dosyalarda*
