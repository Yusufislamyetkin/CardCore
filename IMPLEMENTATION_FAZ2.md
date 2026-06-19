# FAZ 2 — IAM SERVICE (Kimlik Doğrulama & Yetkilendirme)

> **ÖN KOŞUL:** Faz 0 ve Faz 1 tamamen tamamlanmış olmalı.
> **PORT:** 5001
> **VERİTABANI:** iam_db (PostgreSQL)

**AMAÇ:** Kullanıcı kaydı, login, JWT token üretme, role-based yetkilendirme. Bu servis diğer tüm servislerin güvenlik temeli — API Gateway bu servisten alınan JWT'yi doğrular.

**BU FAZIN SONUNDA:**
- `POST /api/auth/register` → yeni kullanıcı kaydı
- `POST /api/auth/login` → JWT access token + refresh token
- `POST /api/auth/refresh` → yeni access token
- JWT'de rol claim'leri var (Admin, Customer, Operator)
- Şifre Argon2id ile hashlenmiş
- Tüm endpoint'ler çalışıyor ve test edilmiş

---

## ADIM 2.0 — Klasör Yapısı ve NuGet Paketleri

**KOMUTLAR:**

```powershell
# Klasör yapısı
mkdir src\Services\MiniBank.IamService\Domain
mkdir src\Services\MiniBank.IamService\Domain\Entities
mkdir src\Services\MiniBank.IamService\Domain\Enums
mkdir src\Services\MiniBank.IamService\Infrastructure
mkdir src\Services\MiniBank.IamService\Infrastructure\Persistence
mkdir src\Services\MiniBank.IamService\Application
mkdir src\Services\MiniBank.IamService\Application\Commands
mkdir src\Services\MiniBank.IamService\Application\Queries
mkdir src\Services\MiniBank.IamService\Api
mkdir src\Services\MiniBank.IamService\Api\Controllers
mkdir src\Services\MiniBank.IamService\Api\Dtos

# NuGet paketleri
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Microsoft.EntityFrameworkCore
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package System.IdentityModel.Tokens.Jwt
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Microsoft.IdentityModel.Tokens
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Konscious.Security.Cryptography.Argon2
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Swashbuckle.AspNetCore
dotnet add src\Services\MiniBank.IamService\MiniBank.IamService.csproj package Serilog.AspNetCore
```

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
```

- [ ] Tamamlandı

---

## ADIM 2.1 — Role Enum

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Domain\Enums\Role.cs`

```csharp
namespace MiniBank.IamService.Domain.Enums;

public enum Role
{
    Customer = 1,    // Normal müşteri — kendi hesaplarına erişir
    Operator = 2,    // Banka operatörü — müşteri bilgilerini görür
    Admin = 3        // Sistem yöneticisi — her şeye erişir
}
```

- [ ] Tamamlandı

---

## ADIM 2.2 — User Entity

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Domain\Entities\User.cs`

```csharp
using MiniBank.IamService.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.IamService.Domain.Entities;

public sealed class User : Entity<UserId>
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Role Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    // EF Core parameterless constructor
    private User() { }

    public static User Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        Role role = Role.Customer)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        var user = new User
        {
            Id = UserId.New(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Role = role,
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };

        return user;
    }

    /// <summary>
    /// Başarılı login sonrası çağrılır.
    /// </summary>
    public void RecordSuccessfulLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
        MarkModified();
    }

    /// <summary>
    /// Başarısız login sonrası çağrılır.
    /// 5 başarısız deneme → 15 dakika kilitle.
    /// </summary>
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
        {
            LockedUntil = DateTime.UtcNow.AddMinutes(15);
        }
        MarkModified();
    }

    /// <summary>
    /// Hesap kilitli mi kontrol eder.
    /// </summary>
    public bool IsLocked(DateTime utcNow)
    {
        return LockedUntil.HasValue && LockedUntil.Value > utcNow;
    }

    public string FullName => $"{FirstName} {LastName}";
}
```

- [ ] Tamamlandı

---

## ADIM 2.3 — Refresh Token Entity

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Domain\Entities\RefreshToken.cs`

```csharp
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.IamService.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public UserId UserId { get; set; } = default!;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);
}
```

- [ ] Tamamlandı

---

## ADIM 2.4 — IAM DbContext

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Infrastructure\Persistence\IamDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using MiniBank.IamService.Domain.Entities;
using MiniBank.IamService.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.IamService.Infrastructure.Persistence;

public sealed class IamDbContext : BaseDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public IamDbContext(DbContextOptions<IamDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === User tablosu ===
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(e => e.Id);

            // UserId → Guid dönüşümü
            entity.Property(e => e.Id)
                .HasConversion(
                    id => id.Value,
                    guid => UserId.From(guid))
                .HasColumnName("id");

            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();

            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();

            entity.Property(e => e.Role)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(e => e.Version).IsConcurrencyToken();

            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        // === RefreshToken tablosu ===
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .HasConversion(
                    id => id.Value,
                    guid => UserId.From(guid))
                .HasColumnName("user_id");

            entity.Property(e => e.Token).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();

            entity.Property(e => e.ExpiresAt).IsRequired();
        });
    }
}
```

- [ ] Tamamlandı

---

## ADIM 2.5 — Password Hasher (Argon2id)

**NEDEN Argon2id:** BCrypt yerine Argon2id tercih ediyoruz çünkü:
- GPU brute-force'a karşı daha dayanıklı (memory-hard)
- OWASP tarafından önerilen algoritma
- Bankacılıkta şifre hash'i için endüstri standardı

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Application\IPasswordHasher.cs`

```csharp
namespace MiniBank.IamService.Application;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Infrastructure\Argon2PasswordHasher.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using MiniBank.IamService.Application;

namespace MiniBank.IamService.Infrastructure;

/// <summary>
/// Argon2id şifre hash'leme.
/// Format: "$argon2id$salt_base64$hash_base64"
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;     // 128 bit
    private const int HashSize = 32;     // 256 bit
    private const int Iterations = 4;    // OWASP önerisi
    private const int MemorySize = 65536; // 64 MB
    private const int Parallelism = 2;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.Iterations = Iterations;
        argon2.MemorySize = MemorySize;
        argon2.DegreeOfParallelism = Parallelism;

        var hash = argon2.GetBytes(HashSize);

        return $"$argon2id${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "argon2id")
            return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.Iterations = Iterations;
        argon2.MemorySize = MemorySize;
        argon2.DegreeOfParallelism = Parallelism;

        var computedHash = argon2.GetBytes(HashSize);

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }
}
```

- [ ] Tamamlandı

---

## ADIM 2.6 — JWT Token Service

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Application\IJwtTokenService.cs`

```csharp
using MiniBank.IamService.Domain.Entities;

namespace MiniBank.IamService.Application;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Infrastructure\JwtTokenService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MiniBank.IamService.Application;
using MiniBank.IamService.Domain.Entities;

namespace MiniBank.IamService.Infrastructure;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Access token üretir. İçinde:
    /// - sub: user ID
    /// - email: kullanıcı email'i
    /// - role: kullanıcı rolü (RBAC için)
    /// - name: tam ad
    /// Süre: 15 dakika (kısa — güvenlik için)
    /// </summary>
    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]
                ?? throw new InvalidOperationException("JWT secret not configured.")));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "MiniBank",
            audience: _config["Jwt:Audience"] ?? "MiniBank",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Refresh token üretir. Opaque string (JWT değil).
    /// Veritabanında saklanır, 7 gün geçerli.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}
```

- [ ] Tamamlandı

---

## ADIM 2.7 — API DTO'ları

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Api\Dtos\RegisterRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.IamService.Api.Dtos;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)]
    string Email,

    [Required, MinLength(8), MaxLength(128)]
    string Password,

    [Required, MaxLength(100)]
    string FirstName,

    [Required, MaxLength(100)]
    string LastName
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Api\Dtos\LoginRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.IamService.Api.Dtos;

public sealed record LoginRequest(
    [Required, EmailAddress]
    string Email,

    [Required]
    string Password
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Api\Dtos\RefreshRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MiniBank.IamService.Api.Dtos;

public sealed record RefreshRequest(
    [Required]
    string RefreshToken
);
```

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Api\Dtos\AuthResponse.cs`

```csharp
namespace MiniBank.IamService.Api.Dtos;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Email,
    string FullName,
    string Role
);
```

- [ ] Tamamlandı

---

## ADIM 2.8 — Auth Controller

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\Api\Controllers\AuthController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.IamService.Api.Dtos;
using MiniBank.IamService.Application;
using MiniBank.IamService.Domain.Entities;
using MiniBank.IamService.Infrastructure.Persistence;

namespace MiniBank.IamService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IamDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        IamDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Yeni kullanıcı kaydı.
    /// 1. Email zaten var mı kontrol et (unique)
    /// 2. Şifreyi Argon2id ile hashle
    /// 3. User entity oluştur ve DB'ye kaydet
    /// 4. JWT access token + refresh token üret ve dön
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        // Email kontrolü
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == request.Email.ToLowerInvariant(), ct);

        if (emailExists)
            return Conflict(new { error = "EMAIL_EXISTS", message = "This email is already registered." });

        // User oluştur
        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.Email, passwordHash, request.FirstName, request.LastName);

        _db.Users.Add(user);

        // Refresh token oluştur
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = _jwtTokenService.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);

        return Created("", new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken.Token,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString()
        ));
    }

    /// <summary>
    /// Kullanıcı girişi.
    /// 1. Email ile kullanıcıyı bul
    /// 2. Hesap kilitli mi kontrol et
    /// 3. Şifreyi doğrula
    /// 4. Başarılı → token üret, başarısız → failed attempt sayacını artır
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant(), ct);

        if (user is null)
            return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Invalid email or password." });

        if (!user.IsActive)
            return Unauthorized(new { error = "ACCOUNT_DISABLED", message = "Account is disabled." });

        if (user.IsLocked(DateTime.UtcNow))
            return Unauthorized(new { error = "ACCOUNT_LOCKED", message = "Account is temporarily locked. Try again later." });

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Invalid email or password." });
        }

        // Başarılı login
        user.RecordSuccessfulLogin();

        // Eski refresh token'ları iptal et
        var oldTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in oldTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        // Yeni refresh token oluştur
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = _jwtTokenService.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);

        return Ok(new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken.Token,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString()
        ));
    }

    /// <summary>
    /// Access token süresi dolduğunda refresh token ile yeni access token al.
    /// 1. Refresh token'ı DB'de bul
    /// 2. Geçerli mi, revoke edilmiş mi kontrol et
    /// 3. Yeni access token + yeni refresh token (rotation) üret
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (storedToken is null || !storedToken.IsActive(DateTime.UtcNow))
            return Unauthorized(new { error = "INVALID_REFRESH_TOKEN", message = "Refresh token is invalid or expired." });

        // Eski token'ı revoke et (token rotation)
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == storedToken.UserId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "USER_NOT_FOUND", message = "User not found or disabled." });

        // Yeni refresh token
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = _jwtTokenService.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(newRefreshToken);

        await _db.SaveChangesAsync(ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);

        return Ok(new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: newRefreshToken.Token,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString()
        ));
    }
}
```

- [ ] Tamamlandı

---

## ADIM 2.9 — appsettings.json

**DOSYA OLUŞTUR:** `src\Services\MiniBank.IamService\appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "IamDb": "Host=localhost;Port=5432;Database=iam_db;Username=minibank;Password=minibank_dev_2024"
  },
  "Jwt": {
    "Secret": "MiniBank-Super-Secret-Key-Must-Be-At-Least-32-Characters-Long-2024",
    "Issuer": "MiniBank.IamService",
    "Audience": "MiniBank"
  }
}
```

**ÖNEMLİ:** Production'da bu secret environment variable'dan gelmelidir. Bu sadece geliştirme ortamı için.

- [ ] Tamamlandı

---

## ADIM 2.10 — Program.cs Güncelleme

**DOSYA ÜSTÜNE YAZ:** `src\Services\MiniBank.IamService\Program.cs`

Bu dosyanın İÇERİĞİNİ TAMAMEN DEĞİŞTİR. Eski /health endpoint'ini koru ve yeni servisleri ekle.

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniBank.IamService.Application;
using MiniBank.IamService.Infrastructure;
using MiniBank.IamService.Infrastructure.Persistence;
using MiniBank.Shared.Infrastructure.Api;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5001");

// === DI Registrations ===

// EF Core — PostgreSQL
builder.Services.AddDbContext<IamDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IamDb")));

// Password hasher
builder.Services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

// JWT token service
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret not configured.");

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
            ClockSkew = TimeSpan.Zero // Token süresi tam olarak dolunca expire olsun
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// === Middleware Pipeline ===

app.UseMiddleware<GlobalExceptionHandler>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "MiniBank.IamService",
    timestamp = DateTime.UtcNow
}));

// === Auto-migrate on startup (sadece development) ===
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

- [ ] Tamamlandı

---

## ADIM 2.11 — EF Core Migration Oluşturma

**KOMUTLAR:**

```powershell
# Migration oluştur
dotnet ef migrations add InitialCreate --project src\Services\MiniBank.IamService --startup-project src\Services\MiniBank.IamService --output-dir Infrastructure\Persistence\Migrations

# Migration'ın oluştuğunu doğrula
Get-ChildItem src\Services\MiniBank.IamService\Infrastructure\Persistence\Migrations\
# Beklenen: *_InitialCreate.cs dosyaları
```

**NOT:** `dotnet ef` tool kurulu değilse şu komutu çalıştır:
```powershell
dotnet tool install --global dotnet-ef
```

- [ ] Tamamlandı

---

## ADIM 2.12 — Servisi Çalıştır ve Test Et

**KOMUTLAR:**

```powershell
# 1. Docker'ın çalıştığından emin ol
docker ps

# 2. Servisi başlat (yeni terminal)
dotnet run --project src\Services\MiniBank.IamService
```

**Başka bir terminal'den test et:**

```powershell
# 1. Health check
Invoke-RestMethod http://localhost:5001/health
# Beklenen: status=healthy

# 2. Register
$body = '{"email":"test@minibank.com","password":"Password123!","firstName":"Test","lastName":"User"}'
Invoke-RestMethod -Uri http://localhost:5001/api/auth/register -Method POST -Body $body -ContentType "application/json"
# Beklenen: accessToken, refreshToken, email, fullName, role

# 3. Login
$body = '{"email":"test@minibank.com","password":"Password123!"}'
Invoke-RestMethod -Uri http://localhost:5001/api/auth/login -Method POST -Body $body -ContentType "application/json"
# Beklenen: yeni accessToken ve refreshToken

# 4. Yanlış şifre ile login
$body = '{"email":"test@minibank.com","password":"WrongPassword"}'
Invoke-RestMethod -Uri http://localhost:5001/api/auth/login -Method POST -Body $body -ContentType "application/json"
# Beklenen: 401 Unauthorized, INVALID_CREDENTIALS

# 5. Swagger UI
# Tarayıcıda aç: http://localhost:5001/swagger
```

- [ ] Tamamlandı

---

## ADIM 2.13 — Unit Test Projesi Oluşturma (IAM)

**KOMUTLAR:**

```powershell
dotnet new xunit -n MiniBank.IamService.UnitTests -o tests\Unit\MiniBank.IamService.UnitTests
dotnet sln add tests\Unit\MiniBank.IamService.UnitTests\MiniBank.IamService.UnitTests.csproj

dotnet add tests\Unit\MiniBank.IamService.UnitTests\MiniBank.IamService.UnitTests.csproj reference src\Services\MiniBank.IamService\MiniBank.IamService.csproj
dotnet add tests\Unit\MiniBank.IamService.UnitTests\MiniBank.IamService.UnitTests.csproj package FluentAssertions
dotnet add tests\Unit\MiniBank.IamService.UnitTests\MiniBank.IamService.UnitTests.csproj package NSubstitute

Remove-Item tests\Unit\MiniBank.IamService.UnitTests\UnitTest1.cs -ErrorAction SilentlyContinue
```

**DOSYA OLUŞTUR:** `tests\Unit\MiniBank.IamService.UnitTests\UserTests.cs`

```csharp
using FluentAssertions;
using MiniBank.IamService.Domain.Entities;
using MiniBank.IamService.Domain.Enums;

namespace MiniBank.IamService.UnitTests;

public class UserTests
{
    [Fact]
    public void Create_ValidInput_ShouldCreateUser()
    {
        var user = User.Create("test@test.com", "hash123", "John", "Doe");

        user.Email.Should().Be("test@test.com");
        user.FirstName.Should().Be("John");
        user.Role.Should().Be(Role.Customer);
        user.IsActive.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public void Create_EmailTrimmedAndLowered()
    {
        var user = User.Create(" TEST@Test.COM ", "hash", "A", "B");

        user.Email.Should().Be("test@test.com");
    }

    [Fact]
    public void RecordFailedLogin_5Times_ShouldLock()
    {
        var user = User.Create("test@test.com", "hash", "A", "B");

        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        user.IsLocked(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void RecordSuccessfulLogin_ShouldResetFailedAttempts()
    {
        var user = User.Create("test@test.com", "hash", "A", "B");
        user.RecordFailedLogin();
        user.RecordFailedLogin();

        user.RecordSuccessfulLogin();

        user.FailedLoginAttempts.Should().Be(0);
        user.IsLocked(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void FullName_ShouldCombineFirstAndLast()
    {
        var user = User.Create("test@test.com", "hash", "John", "Doe");

        user.FullName.Should().Be("John Doe");
    }
}
```

**DOSYA OLUŞTUR:** `tests\Unit\MiniBank.IamService.UnitTests\Argon2PasswordHasherTests.cs`

```csharp
using FluentAssertions;
using MiniBank.IamService.Infrastructure;

namespace MiniBank.IamService.UnitTests;

public class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ShouldReturnArgon2idFormat()
    {
        var hash = _hasher.Hash("TestPassword123!");

        hash.Should().StartWith("$argon2id$");
    }

    [Fact]
    public void Verify_CorrectPassword_ShouldReturnTrue()
    {
        var hash = _hasher.Hash("TestPassword123!");

        _hasher.Verify("TestPassword123!", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ShouldReturnFalse()
    {
        var hash = _hasher.Hash("TestPassword123!");

        _hasher.Verify("WrongPassword", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_SamePassword_DifferentSalt_DifferentHash()
    {
        var hash1 = _hasher.Hash("TestPassword123!");
        var hash2 = _hasher.Hash("TestPassword123!");

        hash1.Should().NotBe(hash2); // farklı salt → farklı hash
    }
}
```

**DOĞRULAMA:**
```powershell
dotnet test tests\Unit\MiniBank.IamService.UnitTests
# Beklenen: tüm testler geçer
```

- [ ] Tamamlandı

---

## ADIM 2.14 — Git Commit

```powershell
git add -A
git commit -m "Faz 2: IAM Service - register, login, JWT, Argon2id, refresh token rotation"
```

- [ ] Tamamlandı

---

## ✅ FAZ 2 KABUL KRİTERLERİ

```
□ POST /api/auth/register → 201 Created, access token döner
□ POST /api/auth/login → 200 OK, access token döner
□ POST /api/auth/refresh → yeni token pair döner, eski refresh token revoke edilir
□ Yanlış şifre → 401 Unauthorized döner
□ 5 başarısız deneme → hesap 15 dk kilitlenir
□ Şifre Argon2id ile hashlenmiş (DB'de plain text yok)
□ JWT'de sub, email, role, name claim'leri var
□ Email unique — aynı email ile ikinci kayıt → 409 Conflict
□ Swagger UI çalışıyor (http://localhost:5001/swagger)
□ Migration çalıştı, users ve refresh_tokens tabloları var
□ Unit testler geçiyor (User + PasswordHasher)
□ dotnet build → 0 error
□ Git commit yapıldı
```

**Faz 2 tamamlandıysa, bu satırı işaretle:** ⬜ → ✅

---

*Faz 3 (Account Service) ayrı dosyada: `IMPLEMENTATION_FAZ3.md`*
