# MiniBank — Uygulama Planı (AI-Ready, Adım Adım)

**Proje:** MiniBank Enterprise Banking System  
**Stack:** .NET 8 / C# / PostgreSQL / Kafka / Redis / Docker  
**Çalışma dizini:** `D:\Bankacılık`  
**Durum:** PLANLANMA AŞAMASINDA

> **BU DOKÜMANI KULLANAN YAPAY ZEKAYA NOT:**
> Her adım sırayla yapılmalıdır. Bir adımı atlamayın. Her adımda:
> 1. Önce komutu çalıştırın veya dosyayı oluşturun
> 2. "DOĞRULAMA" bölümündeki komutu çalıştırarak başarılı olduğunu teyit edin
> 3. Hata varsa düzeltin, sonraki adıma geçmeyin
> 4. Faz sonundaki "KABUL KRİTERLERİ"nin hepsini kontrol edin
>
> Dosya yollarında `\` kullanın (Windows). Terminal komutları PowerShell'dir.
> .NET komutları `dotnet` ile çalışır.

---

## GENEL İLERLEME TABLOSU

| Faz | Başlık | Durum |
|-----|--------|-------|
| 0 | Altyapı & Proje İskeleti | ⬜ Başlanmadı |
| 1 | Shared Kernel & Domain Temelleri | ⬜ Başlanmadı |
| 2 | IAM Service | ⬜ Başlanmadı |
| 3 | Account Service | ⬜ Başlanmadı |
| 4 | Card Service | ⬜ Başlanmadı |
| 5 | Authorization Service | ⬜ Başlanmadı |
| 6 | Transaction Service (Ledger) | ⬜ Başlanmadı |
| 7 | Installment Service (Taksit) | ⬜ Başlanmadı |
| 8 | Fraud Service | ⬜ Başlanmadı |
| 9 | EOD Service (Gün Sonu) | ⬜ Başlanmadı |
| 10 | Reconciliation Service | ⬜ Başlanmadı |
| 11 | Notification Service | ⬜ Başlanmadı |
| 12 | API Gateway & Uçtan Uca Test | ⬜ Başlanmadı |
| 13 | Observability & Resilience | ⬜ Başlanmadı |
| 14 | Güvenlik Sertleştirme | ⬜ Başlanmadı |
| 15 | Yük Testi & Production Readiness | ⬜ Başlanmadı |

---
---

# ═══════════════════════════════════════════════════════
# FAZ 0 — ALTYAPI & PROJE İSKELETİ
# ═══════════════════════════════════════════════════════

**AMAÇ:** Tüm servislerin üzerine inşa edileceği .NET solution yapısını oluşturmak, Docker ile PostgreSQL/Kafka/Redis ayağa kaldırmak ve her servisin boş bir health check endpoint'i ile çalıştığını doğrulamak.

**ÖN KOŞUL:** Aşağıdakiler yüklü olmalı:
- .NET 8 SDK (`dotnet --version` → 8.x.x)
- Docker Desktop (`docker --version` → çalışıyor)
- Git (`git --version`)

**BU FAZIN SONUNDA:**
- `dotnet build` hatasız çalışır
- `docker-compose up` ile Postgres, Kafka, Redis ayağa kalkar
- Her servis `/health` endpoint'inden `200 OK` döner

---

## ADIM 0.1 — Solution ve Klasör Yapısının Oluşturulması

**NE YAPILACAK:** Boş bir .NET solution oluştur ve proje klasörlerini hazırla.

**KOMUTLAR:** Aşağıdaki komutları `D:\Bankacılık` dizininde sırayla çalıştır:

```powershell
# 1. Solution oluştur
dotnet new sln -n MiniBank -o .

# 2. Klasör yapısını oluştur
mkdir src\Shared
mkdir src\Services
mkdir src\ApiGateway
mkdir tests\Unit
mkdir tests\Integration
mkdir infra
mkdir infra\init-scripts
mkdir docs
mkdir docs\adr
mkdir docs\api
```

**DOĞRULAMA:** Aşağıdaki komut çalıştırıldığında `MiniBank.sln` dosyası görünmeli:
```powershell
Test-Path "D:\Bankacılık\MiniBank.sln"
# Beklenen çıktı: True
```

- [ ] Tamamlandı

---

## ADIM 0.2 — global.json Oluşturma

**NE YAPILACAK:** .NET SDK sürümünü sabitle. Bu, farklı makinelerde aynı SDK sürümünün kullanılmasını garanti eder.

**DOSYA OLUŞTUR:** `D:\Bankacılık\global.json`

```json
{
  "sdk": {
    "version": "8.0.0",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

**AÇIKLAMA:**
- `rollForward: "latestFeature"` → 8.0.x serisinin en son sürümünü kullanır (8.1.x'e atlamaz)
- Bu sayede tüm geliştiriciler aynı major.minor SDK ile çalışır

- [ ] Tamamlandı

---

## ADIM 0.3 — Directory.Build.props Oluşturma

**NE YAPILACAK:** Tüm projelerin ortak build ayarlarını tek bir yerde tanımla. Bu dosya solution root'ta durur ve tüm alt projelere otomatik uygulanır.

**DOSYA OLUŞTUR:** `D:\Bankacılık\Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningLevel>7</WarningLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

**AÇIKLAMA:**
- `Nullable>enable` → null reference hataları derleme zamanında yakalanır
- `TreatWarningsAsErrors>true` → warning bırakılmaz, hepsi düzeltilir
- Bu ayarlar her projenin `.csproj` dosyasında tekrarlanmaz, tek yerden yönetilir

- [ ] Tamamlandı

---

## ADIM 0.4 — Directory.Packages.props Oluşturma (Central Package Management)

**NE YAPILACAK:** Tüm NuGet paket sürümlerini tek bir dosyada yönet. Her projenin `.csproj`'unda sürüm YAZILMAZ — sadece paket adı yazılır, sürüm buradan gelir.

**DOSYA OLUŞTUR:** `D:\Bankacılık\Directory.Packages.props`

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- ASP.NET Core & EF Core -->
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.11" />

    <!-- Dapper (performans-kritik sorgular için) -->
    <PackageVersion Include="Dapper" Version="2.1.35" />

    <!-- Kafka / MassTransit -->
    <PackageVersion Include="MassTransit" Version="8.2.5" />
    <PackageVersion Include="MassTransit.Kafka" Version="8.2.5" />

    <!-- Redis -->
    <PackageVersion Include="StackExchange.Redis" Version="2.8.16" />

    <!-- Event Sourcing -->
    <PackageVersion Include="Marten" Version="7.33.2" />

    <!-- Scheduler -->
    <PackageVersion Include="Quartz" Version="3.13.1" />
    <PackageVersion Include="Quartz.Extensions.Hosting" Version="3.13.1" />

    <!-- Observability -->
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />

    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageVersion Include="Serilog.Sinks.Elasticsearch" Version="10.0.0" />
    <PackageVersion Include="Serilog.Enrichers.CorrelationId" Version="3.0.1" />

    <!-- Resilience -->
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="8.10.0" />
    <PackageVersion Include="Polly" Version="8.5.0" />

    <!-- Security -->
    <PackageVersion Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt" Version="8.3.0" />
    <PackageVersion Include="Microsoft.IdentityModel.Tokens" Version="8.3.0" />

    <!-- API Gateway -->
    <PackageVersion Include="Yarp.ReverseProxy" Version="2.2.0" />

    <!-- Test -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="FluentAssertions" Version="7.0.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Testcontainers" Version="4.1.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.1.0" />
    <PackageVersion Include="Testcontainers.Kafka" Version="4.1.0" />
    <PackageVersion Include="Testcontainers.Redis" Version="4.1.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageVersion Include="Bogus" Version="35.6.1" />

    <!-- Swagger -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.9.0" />

    <!-- Health Checks -->
    <PackageVersion Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.2" />
    <PackageVersion Include="AspNetCore.HealthChecks.Redis" Version="8.0.1" />
    <PackageVersion Include="AspNetCore.HealthChecks.Kafka" Version="8.0.1" />

    <!-- Diğer -->
    <PackageVersion Include="Npgsql" Version="8.0.5" />
    <PackageVersion Include="Konscious.Security.Cryptography.Argon2" Version="1.3.1" />
  </ItemGroup>
</Project>
```

**AÇIKLAMA:**
- Tüm NuGet sürümleri burada. Projelerin `.csproj` dosyalarında `Version` YAZMAYACAKSINIZ.
- `.csproj`'da sadece şunu yazacaksınız: `<PackageReference Include="Dapper" />`
- Bu, sürüm çakışmalarını önler.

- [ ] Tamamlandı

---

## ADIM 0.5 — .gitignore Oluşturma

**NE YAPILACAK:** Git'in izlememesi gereken dosyaları tanımla.

**DOSYA OLUŞTUR:** `D:\Bankacılık\.gitignore`

```
## .NET
bin/
obj/
*.user
*.suo
*.userosscache
*.sln.docstates

## IDE
.vs/
.idea/
*.swp
.vscode/

## OS
Thumbs.db
.DS_Store

## Build
[Dd]ebug/
[Rr]elease/
x64/
x86/

## NuGet
*.nupkg
**/packages/*

## Testler
TestResults/

## Docker
infra/.env

## Secrets
*.pfx
*.key
*.pem
appsettings.*.json
!appsettings.json
!appsettings.Development.json
```

- [ ] Tamamlandı

---

## ADIM 0.6 — Shared Class Library Projelerini Oluşturma

**NE YAPILACAK:** 3 adet shared class library projesi oluştur. Bunlar tüm servisler tarafından kullanılacak ortak kod içerecek.

**KOMUTLAR:** Sırayla çalıştır:

```powershell
# 1. Domain (value objects, base entity, exception'lar)
dotnet new classlib -n MiniBank.Shared.Domain -o src\Shared\MiniBank.Shared.Domain
dotnet sln add src\Shared\MiniBank.Shared.Domain\MiniBank.Shared.Domain.csproj

# 2. Contracts (servisler arası DTO, event şemaları)
dotnet new classlib -n MiniBank.Shared.Contracts -o src\Shared\MiniBank.Shared.Contracts
dotnet sln add src\Shared\MiniBank.Shared.Contracts\MiniBank.Shared.Contracts.csproj

# 3. Infrastructure (EF Core base, Kafka, Redis, Outbox)
dotnet new classlib -n MiniBank.Shared.Infrastructure -o src\Shared\MiniBank.Shared.Infrastructure
dotnet sln add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj
```

**ŞİMDİ REFERANSLARI EKLE:**

```powershell
# Contracts, Domain'e bağımlı
dotnet add src\Shared\MiniBank.Shared.Contracts\MiniBank.Shared.Contracts.csproj reference src\Shared\MiniBank.Shared.Domain\MiniBank.Shared.Domain.csproj

# Infrastructure, hem Domain'e hem Contracts'a bağımlı
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj reference src\Shared\MiniBank.Shared.Domain\MiniBank.Shared.Domain.csproj
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj reference src\Shared\MiniBank.Shared.Contracts\MiniBank.Shared.Contracts.csproj
```

**ŞİMDİ BOŞ Class1.cs dosyalarını sil (otomatik oluşturulmuşlardır):**

```powershell
Remove-Item src\Shared\MiniBank.Shared.Domain\Class1.cs -ErrorAction SilentlyContinue
Remove-Item src\Shared\MiniBank.Shared.Contracts\Class1.cs -ErrorAction SilentlyContinue
Remove-Item src\Shared\MiniBank.Shared.Infrastructure\Class1.cs -ErrorAction SilentlyContinue
```

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
# Beklenen: "Build succeeded" — 0 error, 0 warning
```

- [ ] Tamamlandı

---

## ADIM 0.7 — Servis Projelerini Oluşturma

**NE YAPILACAK:** 10 adet ASP.NET Core Web API projesi + 1 API Gateway projesi oluştur. Her biri boş bir health check endpoint'i ile başlayacak.

**KOMUTLAR:** Her servisi tek tek oluştur. Aşağıdaki komutları sırayla çalıştır:

```powershell
# === SERVİSLER ===

# IAM Service
dotnet new webapi -n MiniBank.IamService -o src\Services\MiniBank.IamService --no-openapi
dotnet sln add src\Services\MiniBank.IamService\MiniBank.IamService.csproj

# Account Service
dotnet new webapi -n MiniBank.AccountService -o src\Services\MiniBank.AccountService --no-openapi
dotnet sln add src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj

# Card Service
dotnet new webapi -n MiniBank.CardService -o src\Services\MiniBank.CardService --no-openapi
dotnet sln add src\Services\MiniBank.CardService\MiniBank.CardService.csproj

# Authorization Service
dotnet new webapi -n MiniBank.AuthorizationService -o src\Services\MiniBank.AuthorizationService --no-openapi
dotnet sln add src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj

# Transaction Service
dotnet new webapi -n MiniBank.TransactionService -o src\Services\MiniBank.TransactionService --no-openapi
dotnet sln add src\Services\MiniBank.TransactionService\MiniBank.TransactionService.csproj

# Installment Service
dotnet new webapi -n MiniBank.InstallmentService -o src\Services\MiniBank.InstallmentService --no-openapi
dotnet sln add src\Services\MiniBank.InstallmentService\MiniBank.InstallmentService.csproj

# Fraud Service
dotnet new webapi -n MiniBank.FraudService -o src\Services\MiniBank.FraudService --no-openapi
dotnet sln add src\Services\MiniBank.FraudService\MiniBank.FraudService.csproj

# EOD Service
dotnet new webapi -n MiniBank.EodService -o src\Services\MiniBank.EodService --no-openapi
dotnet sln add src\Services\MiniBank.EodService\MiniBank.EodService.csproj

# Reconciliation Service
dotnet new webapi -n MiniBank.ReconciliationService -o src\Services\MiniBank.ReconciliationService --no-openapi
dotnet sln add src\Services\MiniBank.ReconciliationService\MiniBank.ReconciliationService.csproj

# Notification Service
dotnet new webapi -n MiniBank.NotificationService -o src\Services\MiniBank.NotificationService --no-openapi
dotnet sln add src\Services\MiniBank.NotificationService\MiniBank.NotificationService.csproj

# === API GATEWAY ===

dotnet new webapi -n MiniBank.ApiGateway -o src\ApiGateway\MiniBank.ApiGateway --no-openapi
dotnet sln add src\ApiGateway\MiniBank.ApiGateway\MiniBank.ApiGateway.csproj
```

**ŞİMDİ HER SERVİSE SHARED REFERANSLARI EKLE:**

Her servis projesi, 3 shared projeye de referans verecek. Aşağıdaki komutları çalıştır:

```powershell
$services = @(
    "src\Services\MiniBank.IamService\MiniBank.IamService.csproj",
    "src\Services\MiniBank.AccountService\MiniBank.AccountService.csproj",
    "src\Services\MiniBank.CardService\MiniBank.CardService.csproj",
    "src\Services\MiniBank.AuthorizationService\MiniBank.AuthorizationService.csproj",
    "src\Services\MiniBank.TransactionService\MiniBank.TransactionService.csproj",
    "src\Services\MiniBank.InstallmentService\MiniBank.InstallmentService.csproj",
    "src\Services\MiniBank.FraudService\MiniBank.FraudService.csproj",
    "src\Services\MiniBank.EodService\MiniBank.EodService.csproj",
    "src\Services\MiniBank.ReconciliationService\MiniBank.ReconciliationService.csproj",
    "src\Services\MiniBank.NotificationService\MiniBank.NotificationService.csproj",
    "src\ApiGateway\MiniBank.ApiGateway\MiniBank.ApiGateway.csproj"
)

foreach ($svc in $services) {
    dotnet add $svc reference src\Shared\MiniBank.Shared.Domain\MiniBank.Shared.Domain.csproj
    dotnet add $svc reference src\Shared\MiniBank.Shared.Contracts\MiniBank.Shared.Contracts.csproj
    dotnet add $svc reference src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj
}
```

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
# Beklenen: "Build succeeded" — 0 error
```

- [ ] Tamamlandı

---

## ADIM 0.8 — Her Servise Health Check Endpoint'i Ekle

**NE YAPILACAK:** Her servisin `Program.cs` dosyasını düzenle. `dotnet new webapi` template'i bazı varsayılan kodlar oluşturur — bunları kaldır ve aşağıdaki minimal yapıyı koy.

**NEDEN:** Her servisin çalıştığını doğrulamak için basit bir endpoint gerekli. Ayrıca her servis FARKLI bir port'ta çalışmalı — çakışmasın.

**HER SERVİS İÇİN AYNI ŞABLONU KULLAN — sadece port ve servis adını değiştir:**

### Port Haritası (EZBERLE — tüm proje boyunca bu portlar kullanılacak):

```
IAM Service:            5001
Account Service:        5002
Card Service:           5003
Authorization Service:  5004
Transaction Service:    5005
Installment Service:    5006
Fraud Service:          5007
EOD Service:            5008
Reconciliation Service: 5009
Notification Service:   5010
API Gateway:            5000
```

### Şablon Program.cs

**AŞAĞIDAKI İÇERİĞİ** her servisin `Program.cs` dosyasına yaz. **Sadece 2 yeri değiştir:**
1. `builder.WebHost.UseUrls("http://localhost:PORT")` → yukarıdaki port haritasındaki portu yaz
2. `serviceName` → servisin adını yaz

**Örnek: IAM Service** — Dosya: `src\Services\MiniBank.IamService\Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5001");

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "MiniBank.IamService",
    timestamp = DateTime.UtcNow
}));

app.Run();
```

**Aynı şablonu şu dosyalara yaz (port ve service adını değiştirerek):**

| Dosya Yolu | Port | Service Adı |
|---|---|---|
| `src\Services\MiniBank.IamService\Program.cs` | 5001 | MiniBank.IamService |
| `src\Services\MiniBank.AccountService\Program.cs` | 5002 | MiniBank.AccountService |
| `src\Services\MiniBank.CardService\Program.cs` | 5003 | MiniBank.CardService |
| `src\Services\MiniBank.AuthorizationService\Program.cs` | 5004 | MiniBank.AuthorizationService |
| `src\Services\MiniBank.TransactionService\Program.cs` | 5005 | MiniBank.TransactionService |
| `src\Services\MiniBank.InstallmentService\Program.cs` | 5006 | MiniBank.InstallmentService |
| `src\Services\MiniBank.FraudService\Program.cs` | 5007 | MiniBank.FraudService |
| `src\Services\MiniBank.EodService\Program.cs` | 5008 | MiniBank.EodService |
| `src\Services\MiniBank.ReconciliationService\Program.cs` | 5009 | MiniBank.ReconciliationService |
| `src\Services\MiniBank.NotificationService\Program.cs` | 5010 | MiniBank.NotificationService |
| `src\ApiGateway\MiniBank.ApiGateway\Program.cs` | 5000 | MiniBank.ApiGateway |

**ÖNEMLİ:** `dotnet new webapi` template'inin oluşturduğu örnek endpoint'leri (WeatherForecast vb.) SİL. Sadece yukarıdaki `/health` endpoint'i kalsın.

**DOĞRULAMA:** Herhangi bir servisi çalıştırıp test et:
```powershell
# Terminal 1'de:
dotnet run --project src\Services\MiniBank.IamService

# Terminal 2'de (veya tarayıcıda):
Invoke-RestMethod http://localhost:5001/health
# Beklenen: status=healthy, service=MiniBank.IamService
```

- [ ] Tamamlandı

---

## ADIM 0.9 — Docker Compose Dosyasını Oluşturma

**NE YAPILACAK:** PostgreSQL, Kafka, Redis ve Jaeger'ı Docker ile ayağa kaldıracak compose dosyasını oluştur.

**DOSYA OLUŞTUR:** `D:\Bankacılık\infra\.env`

```
POSTGRES_USER=minibank
POSTGRES_PASSWORD=minibank_dev_2024
REDIS_PASSWORD=redis_dev_2024
```

**DOSYA OLUŞTUR:** `D:\Bankacılık\infra\docker-compose.yml`

```yaml
version: '3.9'

services:
  # ──────────────── PostgreSQL ────────────────
  postgres:
    image: postgres:16-alpine
    container_name: minibank-postgres
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: minibank_master
    ports:
      - "5432:5432"
    volumes:
      - minibank-pgdata:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 5s
      timeout: 5s
      retries: 5

  # ──────────────── Apache Kafka (KRaft mode — Zookeeper yok) ────────────────
  kafka:
    image: confluentinc/cp-kafka:7.6.0
    container_name: minibank-kafka
    environment:
      KAFKA_NODE_ID: 1
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT,HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:29092,HOST://localhost:9092
      KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:29092,CONTROLLER://0.0.0.0:29093,HOST://0.0.0.0:9092
      KAFKA_PROCESS_ROLES: broker,controller
      KAFKA_CONTROLLER_QUORUM_VOTERS: 1@kafka:29093
      KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
      KAFKA_INTER_BROKER_LISTENER_NAME: PLAINTEXT
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_NUM_PARTITIONS: 6
      CLUSTER_ID: MiniBank-Kafka-Cluster-001
    ports:
      - "9092:9092"
    healthcheck:
      test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9092"]
      interval: 10s
      timeout: 10s
      retries: 5

  # ──────────────── Redis ────────────────
  redis:
    image: redis:7-alpine
    container_name: minibank-redis
    command: redis-server --requirepass ${REDIS_PASSWORD} --maxmemory 256mb --maxmemory-policy allkeys-lru
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
      interval: 5s
      timeout: 5s
      retries: 5

  # ──────────────── Jaeger (Distributed Tracing UI) ────────────────
  jaeger:
    image: jaegertracing/all-in-one:1.56
    container_name: minibank-jaeger
    environment:
      COLLECTOR_OTLP_ENABLED: true
    ports:
      - "16686:16686"   # Jaeger UI
      - "4317:4317"     # OTLP gRPC
      - "4318:4318"     # OTLP HTTP

volumes:
  minibank-pgdata:
```

**AÇIKLAMA:**
- **PostgreSQL:** Port 5432, kullanıcı/şifre `.env`'den okunur, `init-scripts` klasöründeki SQL'ler otomatik çalışır
- **Kafka:** KRaft mode (Zookeeper yok, daha modern), port 9092 host'tan erişim
- **Redis:** Şifreli, 256MB bellek limiti, port 6379
- **Jaeger:** Distributed tracing UI, port 16686'da web arayüzü

- [ ] Tamamlandı

---

## ADIM 0.10 — Veritabanı Başlatma Script'i

**NE YAPILACAK:** PostgreSQL ilk başladığında her servis için ayrı veritabanı oluşturacak SQL script'ini yaz.

**DOSYA OLUŞTUR:** `D:\Bankacılık\infra\init-scripts\01-create-databases.sql`

```sql
-- Her servisin kendi veritabanı var. Servisler birbirinin DB'sine ERİŞEMEZ.
-- Bu bankacılıkta "database per service" prensibidir.

CREATE DATABASE iam_db;
COMMENT ON DATABASE iam_db IS 'Kimlik doğrulama ve yetkilendirme servisi';

CREATE DATABASE account_db;
COMMENT ON DATABASE account_db IS 'Müşteri ve hesap yönetimi servisi';

CREATE DATABASE card_db;
COMMENT ON DATABASE card_db IS 'Kart yaşam döngüsü ve limit yönetimi servisi';

CREATE DATABASE authorization_db;
COMMENT ON DATABASE authorization_db IS 'Ödeme onay ve provizyon servisi';

CREATE DATABASE transaction_db;
COMMENT ON DATABASE transaction_db IS 'İşlem defteri ve çift taraflı muhasebe servisi';

CREATE DATABASE installment_db;
COMMENT ON DATABASE installment_db IS 'Taksit planı ve hesaplama servisi';

CREATE DATABASE fraud_db;
COMMENT ON DATABASE fraud_db IS 'Dolandırıcılık tespit ve kural motoru servisi';

CREATE DATABASE eod_db;
COMMENT ON DATABASE eod_db IS 'Gün sonu batch işlemleri servisi';

CREATE DATABASE reconcile_db;
COMMENT ON DATABASE reconcile_db IS 'Mutabakat servisi';
```

- [ ] Tamamlandı

---

## ADIM 0.11 — Docker Compose'u Çalıştır ve Doğrula

**NE YAPILACAK:** Docker ile tüm bağımlılıkları ayağa kaldır.

**KOMUTLAR:**

```powershell
# Docker Compose'u başlat (infra klasöründen)
docker compose -f infra\docker-compose.yml --env-file infra\.env up -d
```

**DOĞRULAMA — 4 kontrol yap:**

```powershell
# 1. Tüm container'lar çalışıyor mu?
docker ps --format "table {{.Names}}\t{{.Status}}"
# Beklenen: 4 container, hepsi "Up" durumunda

# 2. PostgreSQL bağlantısı çalışıyor mu?
docker exec minibank-postgres psql -U minibank -d minibank_master -c "\l"
# Beklenen: iam_db, account_db, card_db, ... listeleniyor

# 3. Redis çalışıyor mu?
docker exec minibank-redis redis-cli -a redis_dev_2024 ping
# Beklenen: PONG

# 4. Kafka çalışıyor mu?
docker exec minibank-kafka kafka-topics --bootstrap-server localhost:29092 --list
# Beklenen: Boş liste (henüz topic yok) ama hata vermemeli
```

**HATA DURUMUNDA:**
- Container başlamıyorsa: `docker compose -f infra\docker-compose.yml logs <servis-adı>` ile log'a bak
- Port çakışması varsa: o portu kullanan uygulamayı kapat veya compose'daki portu değiştir

- [ ] Tamamlandı

---

## ADIM 0.12 — Test Altyapısı Projelerini Oluşturma

**NE YAPILACAK:** Unit test ve integration test projeleri oluştur.

**KOMUTLAR:**

```powershell
# Unit test projesi (Shared.Domain testleri için)
dotnet new xunit -n MiniBank.Shared.Domain.UnitTests -o tests\Unit\MiniBank.Shared.Domain.UnitTests
dotnet sln add tests\Unit\MiniBank.Shared.Domain.UnitTests\MiniBank.Shared.Domain.UnitTests.csproj

# Integration test base projesi
dotnet new xunit -n MiniBank.Integration.TestBase -o tests\Integration\MiniBank.Integration.TestBase
dotnet sln add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj
```

**REFERANS EKLE:**

```powershell
# Unit test → Domain referansı
dotnet add tests\Unit\MiniBank.Shared.Domain.UnitTests\MiniBank.Shared.Domain.UnitTests.csproj reference src\Shared\MiniBank.Shared.Domain\MiniBank.Shared.Domain.csproj

# Integration test → tüm shared projeler
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj reference src\Shared\MiniBank.Shared.Domain\MiniBank.Shared.Domain.csproj
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj reference src\Shared\MiniBank.Shared.Contracts\MiniBank.Shared.Contracts.csproj
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj reference src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj
```

**TEST PAKETLERİ EKLE:**

```powershell
# Unit test projesine
dotnet add tests\Unit\MiniBank.Shared.Domain.UnitTests\MiniBank.Shared.Domain.UnitTests.csproj package FluentAssertions
dotnet add tests\Unit\MiniBank.Shared.Domain.UnitTests\MiniBank.Shared.Domain.UnitTests.csproj package NSubstitute
dotnet add tests\Unit\MiniBank.Shared.Domain.UnitTests\MiniBank.Shared.Domain.UnitTests.csproj package Bogus

# Integration test projesine
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj package FluentAssertions
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj package Testcontainers
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj package Testcontainers.PostgreSql
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj package Testcontainers.Kafka
dotnet add tests\Integration\MiniBank.Integration.TestBase\MiniBank.Integration.TestBase.csproj package Testcontainers.Redis
```

**BOŞ TEST DOSYALARINI SİL ve basit bir test yaz:**

Unit test dosyasını düzenle: `tests\Unit\MiniBank.Shared.Domain.UnitTests\UnitTest1.cs` → Sil ve yerine oluştur:

**DOSYA OLUŞTUR:** `tests\Unit\MiniBank.Shared.Domain.UnitTests\SmokeTest.cs`

```csharp
using FluentAssertions;

namespace MiniBank.Shared.Domain.UnitTests;

public class SmokeTest
{
    [Fact]
    public void Solution_ShouldBuild_AndTestsShouldRun()
    {
        // Bu test sadece build ve test altyapısının çalıştığını doğrular.
        // Faz 1'de gerçek domain testleri eklenecek.
        var result = 1 + 1;
        result.Should().Be(2);
    }
}
```

```powershell
# Eski dosyayı sil
Remove-Item tests\Unit\MiniBank.Shared.Domain.UnitTests\UnitTest1.cs -ErrorAction SilentlyContinue
Remove-Item tests\Integration\MiniBank.Integration.TestBase\UnitTest1.cs -ErrorAction SilentlyContinue
```

**DOĞRULAMA:**
```powershell
dotnet test MiniBank.sln
# Beklenen: "Passed! - Failed: 0, Passed: 1"
```

- [ ] Tamamlandı

---

## ADIM 0.13 — Git Repository Başlat

**NE YAPILACAK:** Git repo'su oluştur ve ilk commit'i yap.

**KOMUTLAR:**

```powershell
git init
git add -A
git commit -m "Faz 0: Proje iskeleti, Docker Compose, 11 servis ve test altyapisi"
```

- [ ] Tamamlandı

---

## ADIM 0.14 — Son Build ve Doğrulama

**KOMUTLAR:**

```powershell
# 1. Tam build
dotnet build MiniBank.sln
# Beklenen: "Build succeeded" — 0 error, 0 warning

# 2. Testler
dotnet test MiniBank.sln
# Beklenen: "Passed! - Failed: 0, Passed: 1"

# 3. Docker servisleri çalışıyor mu?
docker ps --format "table {{.Names}}\t{{.Status}}"
# Beklenen: 4 container, hepsi Up
```

- [ ] Tamamlandı

---

## ✅ FAZ 0 KABUL KRİTERLERİ

Aşağıdaki HER madde "evet" olmalı. Biri bile "hayır" ise Faz 1'e GEÇMEYİN.

```
□ MiniBank.sln dosyası var ve dotnet build hatasız çalışıyor
□ 14 proje solution'da var (3 shared + 10 servis + 1 gateway)
□ Her servis farklı portta çalışıyor (5000-5010)
□ Her servisin /health endpoint'i 200 OK dönüyor
□ Docker: PostgreSQL çalışıyor, 9 veritabanı oluşturuldu
□ Docker: Kafka çalışıyor (KRaft mode)
□ Docker: Redis çalışıyor (şifreli)
□ Docker: Jaeger çalışıyor (http://localhost:16686)
□ dotnet test → en az 1 test geçiyor
□ Git: İlk commit yapıldı
□ Hiçbir warning yok (TreatWarningsAsErrors aktif)
```

**Faz 0 tamamlandıysa, bu satırı işaretle:** ⬜ → ✅

---
---

# ═══════════════════════════════════════════════════════
# FAZ 1 — SHARED KERNEL & DOMAIN TEMELLERİ
# ═══════════════════════════════════════════════════════

**AMAÇ:** Tüm servislerin ortaklaşa kullanacağı domain yapı taşlarını (value object, base entity, exception, event altyapısı) ve infrastructure bileşenlerini (Outbox, Kafka, Redis, Idempotency) yazmak.

**ÖN KOŞUL:** Faz 0 tamamlanmış, `dotnet build` hatasız.

**BU FAZIN SONUNDA:**
- `Money`, `Pan`, `Iban`, `NationalId` value object'leri çalışır ve test edilmiştir
- Base entity ve aggregate root sınıfları hazırdır
- Outbox pattern ile Kafka'ya event gönderme çalışır
- Redis cache ve Idempotency middleware çalışır
- PAN log scrubber logda kart numarasını gizler

**YAKLAŞIM:** Bu faz çok sayıda dosya içerir. Her dosyanın tam yolunu, tam içeriğini ve testini aşağıda bulacaksınız. Klasör yapısını oluşturmadan önce önce tüm klasörleri oluşturun.

---

## ADIM 1.0 — Klasör Yapısını Oluştur

**KOMUTLAR:**

```powershell
# Domain projesinde klasörler
mkdir src\Shared\MiniBank.Shared.Domain\ValueObjects
mkdir src\Shared\MiniBank.Shared.Domain\Primitives
mkdir src\Shared\MiniBank.Shared.Domain\Enums
mkdir src\Shared\MiniBank.Shared.Domain\Exceptions

# Contracts projesinde klasörler
mkdir src\Shared\MiniBank.Shared.Contracts\Events
mkdir src\Shared\MiniBank.Shared.Contracts\Dtos

# Infrastructure projesinde klasörler
mkdir src\Shared\MiniBank.Shared.Infrastructure\Persistence
mkdir src\Shared\MiniBank.Shared.Infrastructure\Messaging
mkdir src\Shared\MiniBank.Shared.Infrastructure\Caching
mkdir src\Shared\MiniBank.Shared.Infrastructure\Idempotency
mkdir src\Shared\MiniBank.Shared.Infrastructure\Logging
mkdir src\Shared\MiniBank.Shared.Infrastructure\Clock
mkdir src\Shared\MiniBank.Shared.Infrastructure\Api

# Test projesinde klasörler
mkdir tests\Unit\MiniBank.Shared.Domain.UnitTests\ValueObjects
mkdir tests\Unit\MiniBank.Shared.Domain.UnitTests\Primitives
```

- [ ] Tamamlandı

---

## ADIM 1.1 — Currency Enum

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\Currency.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

/// <summary>
/// ISO 4217 para birimi kodları.
/// Bankacılıkta her parasal işlemde currency belirtmek ZORUNLUDUR.
/// </summary>
public enum Currency
{
    TRY = 949,
    USD = 840,
    EUR = 978,
    GBP = 826
}
```

**AÇIKLAMA:** Enum değerleri ISO 4217 numeric code'dur. Bu standart tüm bankacılık sistemlerinde kullanılır.

- [ ] Tamamlandı

---

## ADIM 1.2 — Money Value Object

**NEDEN ÖNEMLİ:** Bankacılıkta `decimal amount` tek başına asla kullanılmaz. Para her zaman bir tutar + para birimi çiftidir. `1000` yazdığında TRY mi USD mi belli değildir. `Money` value object bunu garanti altına alır.

**KURALLAR:**
- `Money` immutable olmalı (bir kez oluşturulunca değiştirilemez)
- İki farklı currency'deki Money toplanamaz (TRY + USD = HATA)
- Amount asla `float` veya `double` OLMAZ — her zaman `decimal` (financial precision)
- Negatif tutar olamaz (negatif para mantıksızdır; çıkış DR/CR ile yönetilir)

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\ValueObjects\Money.cs`

```csharp
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;

namespace MiniBank.Shared.Domain.ValueObjects;

/// <summary>
/// Parasal tutar + para birimi çifti. Bankacılıkta her parasal değer bu tipte taşınır.
/// Immutable: bir kez oluşturulunca değişmez. Yeni değer için yeni Money oluşturulur.
/// </summary>
public sealed record Money
{
    /// <summary>
    /// Tutar. Her zaman >= 0. Negatif tutar kabul edilmez.
    /// decimal kullanılır çünkü float/double yuvarlama hatası yapar.
    /// Örnek: 0.1 + 0.2 != 0.3 (float ile). decimal ile = 0.3 (doğru).
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// ISO 4217 para birimi kodu.
    /// </summary>
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));

        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Sıfır tutarlı para oluşturur. Hesap açılışında başlangıç bakiyesi için kullanılır.
    /// </summary>
    public static Money Zero(Currency currency) => new(0, currency);

    /// <summary>
    /// İki parayı toplar. Currency'ler aynı OLMALI.
    /// Farklı currency'de toplama → CurrencyMismatchException fırlatır.
    /// </summary>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>
    /// Bir paradan diğerini çıkarır. Sonuç negatife düşerse exception fırlatır.
    /// </summary>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        var result = left.Amount - right.Amount;
        if (result < 0)
            throw new InsufficientFundsException(left, right);
        return new Money(result, left.Currency);
    }

    /// <summary>
    /// Sabit çarpanla çarpma. Örnek: taksit hesaplamada faiz oranı ile çarpma.
    /// </summary>
    public static Money operator *(Money money, decimal multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentException("Multiplier cannot be negative.", nameof(multiplier));
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public override string ToString() => $"{Amount:N2} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new CurrencyMismatchException(left.Currency, right.Currency);
    }
}
```

- [ ] Tamamlandı

---

## ADIM 1.3 — Domain Exception'lar

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Exceptions\DomainException.cs`

```csharp
namespace MiniBank.Shared.Domain.Exceptions;

/// <summary>
/// Tüm domain exception'larının base class'ı.
/// Business kuralı ihlal edildiğinde fırlatılır (teknik hata DEĞİL).
/// HTTP'de 422 Unprocessable Entity döner.
/// </summary>
public abstract class DomainException : Exception
{
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Exceptions\CurrencyMismatchException.cs`

```csharp
using MiniBank.Shared.Domain.Enums;

namespace MiniBank.Shared.Domain.Exceptions;

public sealed class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException(Currency expected, Currency actual)
        : base("CURRENCY_MISMATCH", $"Currency mismatch: expected {expected}, got {actual}.")
    {
    }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Exceptions\InsufficientFundsException.cs`

```csharp
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.Exceptions;

public sealed class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(Money available, Money requested)
        : base("INSUFFICIENT_FUNDS",
            $"Insufficient funds: available {available}, requested {requested}.")
    {
    }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Exceptions\InvalidStateTransitionException.cs`

```csharp
namespace MiniBank.Shared.Domain.Exceptions;

public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string entityType, string fromState, string toState)
        : base("INVALID_STATE_TRANSITION",
            $"{entityType}: transition from '{fromState}' to '{toState}' is not allowed.")
    {
    }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Exceptions\CardNotActiveException.cs`

```csharp
namespace MiniBank.Shared.Domain.Exceptions;

public sealed class CardNotActiveException : DomainException
{
    public CardNotActiveException(string cardId)
        : base("CARD_NOT_ACTIVE", $"Card '{cardId}' is not in Active status.")
    {
    }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Exceptions\AccountFrozenException.cs`

```csharp
namespace MiniBank.Shared.Domain.Exceptions;

public sealed class AccountFrozenException : DomainException
{
    public AccountFrozenException(string accountId)
        : base("ACCOUNT_FROZEN", $"Account '{accountId}' is frozen. No operations allowed.")
    {
    }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Exceptions\ConcurrencyConflictException.cs`

```csharp
namespace MiniBank.Shared.Domain.Exceptions;

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string entityType, string entityId)
        : base("CONCURRENCY_CONFLICT",
            $"{entityType} '{entityId}' was modified by another process. Please retry.")
    {
    }
}
```

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
# Beklenen: Build succeeded, 0 error, 0 warning
```

- [ ] Tamamlandı

---

## ADIM 1.4 — Money Unit Testleri

**DOSYA OLUŞTUR:** `tests\Unit\MiniBank.Shared.Domain.UnitTests\ValueObjects\MoneyTests.cs`

```csharp
using FluentAssertions;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldSucceed()
    {
        var money = new Money(100.50m, Currency.TRY);

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be(Currency.TRY);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        var act = () => new Money(-1, Currency.TRY);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        var a = new Money(100, Currency.TRY);
        var b = new Money(50, Currency.TRY);

        var result = a + b;

        result.Amount.Should().Be(150);
        result.Currency.Should().Be(Currency.TRY);
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrowCurrencyMismatch()
    {
        var try_ = new Money(100, Currency.TRY);
        var usd = new Money(50, Currency.USD);

        var act = () => try_ + usd;

        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Subtract_SufficientFunds_ShouldReturnDifference()
    {
        var a = new Money(100, Currency.TRY);
        var b = new Money(30, Currency.TRY);

        var result = a - b;

        result.Amount.Should().Be(70);
    }

    [Fact]
    public void Subtract_InsufficientFunds_ShouldThrow()
    {
        var a = new Money(30, Currency.TRY);
        var b = new Money(100, Currency.TRY);

        var act = () => a - b;

        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void Multiply_ShouldReturnProduct()
    {
        var money = new Money(100, Currency.TRY);

        var result = money * 1.5m;

        result.Amount.Should().Be(150);
    }

    [Fact]
    public void Zero_ShouldReturnZeroAmount()
    {
        var zero = Money.Zero(Currency.TRY);

        zero.Amount.Should().Be(0);
        zero.Currency.Should().Be(Currency.TRY);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var money = new Money(1500.75m, Currency.TRY);

        money.ToString().Should().Contain("1");
        money.ToString().Should().Contain("TRY");
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_ShouldBeEqual()
    {
        var a = new Money(100, Currency.TRY);
        var b = new Money(100, Currency.TRY);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentAmount_ShouldNotBeEqual()
    {
        var a = new Money(100, Currency.TRY);
        var b = new Money(200, Currency.TRY);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Comparison_GreaterThan_ShouldWork()
    {
        var big = new Money(200, Currency.TRY);
        var small = new Money(100, Currency.TRY);

        (big > small).Should().BeTrue();
        (small > big).Should().BeFalse();
    }

    [Fact]
    public void Comparison_DifferentCurrency_ShouldThrow()
    {
        var try_ = new Money(100, Currency.TRY);
        var usd = new Money(100, Currency.USD);

        var act = () => try_ > usd;

        act.Should().Throw<CurrencyMismatchException>();
    }
}
```

**DOĞRULAMA:**
```powershell
dotnet test tests\Unit\MiniBank.Shared.Domain.UnitTests
# Beklenen: Passed! — 13 test geçmeli, 0 failed
```

- [ ] Tamamlandı

---

## ADIM 1.5'ten itibaren devam edecek konular (bu dosya büyüdükçe):

Aşağıdaki adımlar aynı detay seviyesinde yazılacaktır. Her adım için tam dosya yolu, tam kod, tam test ve doğrulama komutu verilecektir.

### Faz 1'in kalan adımları (her biri yukarıdaki format ve detayda):

- [ ] **1.5** — Strongly Typed ID'ler (`AccountId`, `CardId`, `TransactionId`, vb.)
- [ ] **1.6** — `Pan` value object (Luhn algoritması, maskeleme, hash)
- [ ] **1.7** — `Iban` value object (TR formatı, MOD 97 doğrulama)
- [ ] **1.8** — `NationalId` value object (TC Kimlik No, algoritmik doğrulama)
- [ ] **1.9** — `CardExpiry` value object
- [ ] **1.10** — Tüm enum'lar (`AccountType`, `CardType`, `CardStatus`, `TransactionStatus`, vb.)
- [ ] **1.11** — `Entity<TId>` ve `AggregateRoot<TId>` base class'ları
- [ ] **1.12** — `IDomainEvent` interface ve `IntegrationEvent` base record
- [ ] **1.13** — `Result<T>` pattern
- [ ] **1.14** — `IClock` / `SystemClock` / `FakeClock` (zaman abstraction'ı)
- [ ] **1.15** — EF Core `BaseDbContext` (audit fields, optimistic concurrency, outbox)
- [ ] **1.16** — EF Core Value Object dönüştürücüleri (Money → decimal, ID → Guid)
- [ ] **1.17** — Outbox pattern (entity, repository, BackgroundService worker)
- [ ] **1.18** — Kafka producer/consumer altyapısı (MassTransit)
- [ ] **1.19** — Redis `ICacheService` ve implementasyonu
- [ ] **1.20** — Idempotency middleware
- [ ] **1.21** — Global exception handler (DomainException → RFC 7807)
- [ ] **1.22** — PAN log scrubber (Serilog middleware)
- [ ] **1.23** — Correlation ID middleware
- [ ] **1.24** — Cursor-based pagination helper
- [ ] **1.25** — Tüm unit ve integration testler
- [ ] **1.26** — Build doğrulama ve git commit

---

### Faz 2-15 kalan adımları (aynı detay seviyesinde):

Her faz, yukarıdaki Faz 0 ve Faz 1 formatında yazılacaktır:
- Tam dosya yolu
- Tam kod içeriği
- Neden bu şekilde yapıldığının açıklaması
- PowerShell doğrulama komutu
- Unit test kodu
- Kabul kriterleri

**DEVAM NOTLARI:**

Bu doküman çok büyük olduğu için FAZ 1'in kalan adımları ve FAZ 2-15 ayrı dosyalara bölünecektir:

```
IMPLEMENTATION_PLAN.md          ← Bu dosya (Faz 0 + Faz 1 başlangıcı)
IMPLEMENTATION_FAZ1_DEVAM.md    ← Faz 1 kalan adımlar (1.5 - 1.26)
IMPLEMENTATION_FAZ2.md          ← IAM Service detaylı adımlar
IMPLEMENTATION_FAZ3.md          ← Account Service detaylı adımlar
IMPLEMENTATION_FAZ4.md          ← Card Service detaylı adımlar
IMPLEMENTATION_FAZ5.md          ← Authorization Service detaylı adımlar
...
```

**Her faz dosyasında aynı format kullanılır:**
1. Tam dosya yolu
2. Tam kod
3. Açıklama (neden bu şekilde)
4. Doğrulama komutu
5. Test kodu
6. Kabul kriterleri

---

*Son güncelleme: —*
