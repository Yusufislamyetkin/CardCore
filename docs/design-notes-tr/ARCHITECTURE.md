# MiniBank — Enterprise Banking System Architecture

**Version:** 1.0.0  
**Stack:** .NET 8, C#, Enterprise PostgreSQL (Patroni HA), Kafka, Redis, Standart Kubernetes (Bulut-Agnostik)  
**Core Banking:** Architecht BOA / Intertech inter-Beyond (BDDK Uyumlu Çekirdek Bankacılık Paketi)  
**Pattern:** Microservices + DDD + Event-Driven + CQRS + Event Sourcing (Append-Only Ledger)  
**Hedef:** Türkiye bankacılık mevzuatına uygun, gerçek banka seviyesinde backend sistemi

---

## İÇİNDEKİLER

1. [Sistem Genel Bakış](#1-sistem-genel-bakış)
2. [Mimari Prensipler](#2-mimari-prensipler)
3. [Bounded Context Haritası](#3-bounded-context-haritası)
4. [Servis Kataloğu](#4-servis-kataloğu)
5. [Veritabanı Mimarisi](#5-veritabanı-mimarisi)
6. [Event Kataloğu (Kafka)](#6-event-kataloğu-kafka)
7. [API Tasarımı](#7-api-tasarımı)
8. [Güvenlik Mimarisi](#8-güvenlik-mimarisi)
9. [Ödeme Akışları](#9-ödeme-akışları)
10. [EOD / Batch Mimarisi](#10-eod--batch-mimarisi)
11. [Resilience Stratejisi](#11-resilience-stratejisi)
12. [Gözlemlenebilirlik (Observability)](#12-gözlemlenebilirlik-observability)
13. [Altyapı ve Deployment](#13-altyapı-ve-deployment)
14. [Geliştirme Yol Haritası](#14-geliştirme-yol-haritası)
15. [Eksik / Geliştirilebilir Alanlar](#15-eksik--geliştirilebilir-alanlar)
16. [Sistem Tasarım Raporları ve RTO/RPO](#16-sistem-tasarım-raporları-ve-rtorpo)

---

## 1. Sistem Genel Bakış

```
┌─────────────────────────────────────────────────────────────────────┐
│                         EXTERNAL CLIENTS                            │
│          Mobile App │ Web App │ Merchant POS │ 3rd Party API        │
└────────────────────────────┬────────────────────────────────────────┘
                             │ HTTPS / TLS 1.3
┌────────────────────────────▼────────────────────────────────────────┐
│                        API GATEWAY (YARP)                           │
│         Rate Limiting │ Auth │ Routing │ Load Balancing             │
│         SSL Termination │ Request Logging │ IP Whitelist            │
└──┬──────────┬──────────┬──────────┬──────────┬──────────┬──────────┘
   │          │          │          │          │          │
┌──▼──┐  ┌───▼──┐  ┌────▼──┐  ┌───▼──┐  ┌───▼──┐  ┌───▼──────────┐
│ IAM │  │ACCT  │  │ CARD  │  │ AUTH │  │ TXN  │  │  INSTALLMENT │
│ SVC │  │ SVC  │  │  SVC  │  │  SVC │  │  SVC │  │     SVC      │
└──┬──┘  └───┬──┘  └────┬──┘  └───┬──┘  └───┬──┘  └───┬──────────┘
   │         │          │         │          │          │
   └─────────┴──────────┴────┬────┴──────────┴──────────┘
                             │
              ┌──────────────▼──────────────┐
              │      KAFKA EVENT BUS        │
              │  (Confluent Platform)       │
              └──────────────┬──────────────┘
                             │
   ┌─────────┬───────────────┼────────────────┬──────────────┐
┌──▼──┐  ┌───▼──┐  ┌────────▼──┐  ┌──────────▼──┐  ┌───────▼──────┐
│FRAUD│  │ EOD  │  │ NOTIF SVC │  │ RECONCILE   │  │  REPORTING   │
│ SVC │  │ SVC  │  │           │  │     SVC     │  │     SVC      │
└─────┘  └──────┘  └───────────┘  └─────────────┘  └──────────────┘
```

---

## 2. Mimari Prensipler

### 2.1 Domain-Driven Design (DDD)

Her servis kendi **Bounded Context**'ine sahiptir. Servisler arası iletişim doğrudan DB erişimi veya direct method call ile değil, **domain event**'ler veya **API çağrıları** ile gerçekleşir.

```
Aggregate Root örnekleri:
- Account        → AccountId etrafında kümelenir
- Card           → CardId etrafında kümelenir
- Transaction    → TransactionId etrafında kümelenir
- Installment    → InstallmentPlanId etrafında kümelenir
```

### 2.2 CQRS (Command Query Responsibility Segregation)

```
Command Side (Yazma):
  POST /accounts          → CreateAccountCommand
  POST /cards             → IssueCardCommand
  POST /authorizations    → AuthorizePaymentCommand

Query Side (Okuma):
  GET  /accounts/{id}           → AccountQuery (Read Model)
  GET  /transactions?accountId= → TransactionHistoryQuery
  GET  /statements/{period}     → StatementQuery
```

Yazma ve okuma modelleri ayrı. Okuma modeli Redis veya ayrı bir read-replica üzerinden döner.

### 2.3 Event Sourcing (Finansal Kayıtlar İçin)

Transaction ve Ledger servisleri event sourcing kullanır. Hiçbir finansal kayıt silinmez veya güncellenmez — yeni event yazılır.

```
Event Store Yapısı:
┌─────────────────────────────────────────────────────┐
│ StreamId (AggregateId) │ Version │ EventType │ Data │
│ txn_001               │    1    │ TxnCreated│ {...} │
│ txn_001               │    2    │ TxnAuth'd │ {...} │
│ txn_001               │    3    │ TxnCleared│ {...} │
│ txn_001               │    4    │ TxnSettled│ {...} │
└─────────────────────────────────────────────────────┘
```

### 2.4 Database per Service

Her mikroservisin kendi veritabanı şeması vardır. Başka servisin DB'sine doğrudan erişim yasaktır.

```
iam_db          → Identity & Access Management
account_db      → Account & Customer
card_db         → Card Management
authorization_db→ Authorization & Pre-auth
transaction_db  → Ledger & Transactions (Event Store)
installment_db  → Installment Plans
fraud_db        → Fraud Rules & Cases
eod_db          → Batch Jobs & EOD State
reconcile_db    → Reconciliation Records
```

---

## 3. Bounded Context Haritası

```
┌────────────────────┐     ┌────────────────────┐
│   CUSTOMER CONTEXT │     │   ACCOUNT CONTEXT  │
│                    │     │                    │
│ - Customer         │────►│ - Account          │
│ - KYC              │     │ - Balance          │
│ - Address          │     │ - AccountType      │
│ - Contact          │     │ - Currency         │
└────────────────────┘     └─────────┬──────────┘
                                     │
          ┌──────────────────────────▼──────────────────────────┐
          │                   CARD CONTEXT                       │
          │                                                      │
          │  - Card (PAN, BIN, CVV hash, Expiry)                │
          │  - CardStatus (Active/Blocked/Cancelled)            │
          │  - CardLimit (SpendingLimit, CashLimit)             │
          │  - CardProduct (Visa/MC/Troy, Credit/Debit/Prepaid) │
          └──────────────────────────┬──────────────────────────┘
                                     │
     ┌───────────────────────────────▼───────────────────────────────┐
     │                    AUTHORIZATION CONTEXT                      │
     │                                                               │
     │  - AuthorizationRequest  (ISO 8583 inbound)                  │
     │  - AuthorizationResponse (Approve/Decline + reason code)     │
     │  - PreAuthorization      (Hold / Block on limit)             │
     │  - AuthorizationReversal (Void / Timeout release)            │
     └───────────────────────────────┬───────────────────────────────┘
                                     │
     ┌───────────────────────────────▼───────────────────────────────┐
     │                    TRANSACTION CONTEXT                        │
     │                                                               │
     │  - LedgerEntry    (Double-entry: Debit + Credit)             │
     │  - Transaction    (Auth → Clear → Settle lifecycle)          │
     │  - Refund         │  Reversal  │  Chargeback                 │
     └───────────────────────────────┬───────────────────────────────┘
                                     │
          ┌──────────────────────────▼──────────────────────────┐
          │                INSTALLMENT CONTEXT                   │
          │                                                      │
          │  - InstallmentPlan   (Ana plan: N taksit, faiz)     │
          │  - InstallmentItem   (Her taksit: tutar, vade)      │
          │  - KKDF / BSMV      (Vergi hesaplama)               │
          │  - CampaignRule     (Ertelemeli, ekstra taksit)     │
          └──────────────────────────────────────────────────────┘
```

---

## 4. Servis Kataloğu

Sistem **10 mikroservis ve bir API Gateway**'den oluşur. Her servisin sorumluluğu ve ilgili detay bölümü aşağıdadır. (SWIFT/uluslararası muhabir bankacılık ve döviz işlemleri bu projenin ilk kapsamı dışındadır; ileride ayrı bir servis olarak eklenebilir.)

| Servis (detay bölümü) | Sorumluluklar |
| :--- | :--- |
| **IAM Service** (4.1) | Kimlik doğrulama ve yetkilendirme; MFA, OAuth2/OpenID Connect, e-Devlet SSO; API key ve RBAC. |
| **Account Service** (4.2) | Müşteri yönetimi (KYC durumu dahil) ve hesap yönetimi; vadesiz/tasarruf/kredi kartı hesabı; bakiye ve hold takibi. |
| **Card Service** (4.3) | Kart yaşam döngüsü (talep→üretim→aktivasyon→iptal); BIN tablosu; harcama/nakit avans/taksit/yurt dışı limitleri. |
| **Authorization Service** (4.4) | ISO 8583 provizyon; atomik limit kontrolü; fraud pre-check; BKM Switch ve TCMB FAST/EFT entegrasyon noktası. |
| **Transaction Service** (4.5) | Çift taraflı muhasebe defteri (Core Ledger / GL rolü); auth→clearing→settlement; refund/reversal/chargeback. |
| **Installment Service** (4.6) | Taksit planı hesaplama (annüite); KKDF/BSMV vergi hesabı; kampanya tipleri. |
| **Fraud Service** (4.7) | Kural motoru, velocity kontrolü, risk skorlama; gerçek zamanlı ve batch fraud analizi. |
| **EOD Service** (4.8) | Gün sonu batch; faiz tahakkuku, ekstre kesimi, asgari ödeme ve gecikme faizi hesaplama. |
| **Reconciliation Service** (4.9) | İç mutabakat ve BKM/Visa/Mastercard clearing dosyası mutabakatı. |
| **Notification Service** (4.10) | Olay tabanlı SMS/e-posta/push bildirimleri. |
| **API Gateway** (YARP) | Tüm servislere tek giriş noktası; rate limiting, WAF, mTLS, token introspection, yönlendirme. |

### 4.1 IAM Service (Identity & Access Management)

**Sorumluluk:** Kimlik doğrulama, yetkilendirme, oturum yönetimi

**Teknoloji:** ASP.NET Core 8, EF Core, PostgreSQL, Redis (session)

**Özellikler:**
- JWT (Access Token: 15 dk, Refresh Token: 30 gün)
- API Key yönetimi (merchant entegrasyonları için)
- Role-Based Access Control (RBAC)
- Multi-Factor Authentication (TOTP)
- Brute-force koruması (Redis sliding window)
- Token revocation (blacklist in Redis)

**Kritik endpoint'ler:**
```
POST /auth/login
POST /auth/refresh
POST /auth/logout
POST /auth/mfa/verify
POST /auth/api-keys          (merchant için)
GET  /auth/introspect        (diğer servisler token doğrular)
```

---

### 4.2 Account Service

**Sorumluluk:** Müşteri ve hesap yönetimi, bakiye takibi

**Teknoloji:** ASP.NET Core 8, EF Core, PostgreSQL, Redis (balance cache)

**Özellikler:**
- Müşteri (Customer) CRUD
- Hesap tipleri: Vadesiz, Tasarruf, Kredi Kartı Hesabı
- Bakiye okuma: Gerçek bakiye vs. kullanılabilir bakiye
- Multi-currency hesap desteği
- Hesap dondurma / bloke işlemleri

**Domain Model:**
```csharp
public class Account
{
    public AccountId Id { get; }
    public CustomerId CustomerId { get; }
    public AccountType Type { get; }           // Current, Savings, CreditCard
    public Currency Currency { get; }
    public Money Balance { get; }              // Defter bakiyesi
    public Money AvailableBalance { get; }     // Kullanılabilir = Balance - Holds
    public AccountStatus Status { get; }
    public IReadOnlyList<Hold> ActiveHolds { get; }
}
```

---

### 4.3 Card Service

**Sorumluluk:** Kart yaşam döngüsü, limit yönetimi, kart ürün konfigürasyonu

**Teknoloji:** ASP.NET Core 8, EF Core, PostgreSQL, Redis (limit cache)

**Özellikler:**
- Kart başvuru ve üretim akışı
- PAN üretimi (Luhn algoritması)
- CVV2 hash'li saklama (asla plaintext)
- BIN tablosu yönetimi (VISA/MC/Troy ayrımı)
- Limit türleri: Harcama, Nakit Avans, Taksit, Yurt Dışı
- Geçici limit artırımı (expiry ile)
- Kart statü geçişleri: Active → TempBlocked → Blocked → Cancelled

**Domain Model:**
```csharp
public class Card
{
    public CardId Id { get; }
    public AccountId AccountId { get; }
    public string MaskedPan { get; }           // **** **** **** 1234
    public string PanHash { get; }             // SHA-256 ile
    public string Bin { get; }                 // İlk 6-8 hane
    public string LastFour { get; }
    public CardType Type { get; }              // Credit, Debit, Prepaid, Virtual
    public CardScheme Scheme { get; }          // Visa, Mastercard, Troy
    public CardStatus Status { get; }
    public DateOnly ExpiryDate { get; }
    public CardLimitSet Limits { get; }
    public IReadOnlyList<CardLimitOverride> TemporaryLimits { get; }
}

public class CardLimitSet
{
    public Money SpendingLimit { get; }        // Toplam harcama limiti
    public Money AvailableSpendingLimit { get; }
    public Money CashAdvanceLimit { get; }
    public Money AvailableCashAdvanceLimit { get; }
    public Money InstallmentLimit { get; }
    public Money AbroadLimit { get; }
}
```

---

### 4.4 Authorization Service

**Sorumluluk:** Ödeme onayı, ISO 8583 mesaj işleme, limit kontrolü, fraud pre-check

**Teknoloji:** ASP.NET Core 8, Dapper (latency kritik), Redis (limit cache), PostgreSQL

**Performans Hedefi:** P99 < 200ms

**Akış:**
```
ISO 8583 Request
      │
      ▼
┌─────────────┐
│ Message     │  ISO 8583 parse, field validation
│ Parser      │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Card        │  Kart var mı? Status Active mi? Expiry geçmemiş mi?
│ Validator   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Fraud       │  Hız kontrolü, kara liste, anomali (sync, <10ms)
│ Pre-Check   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Limit       │  Kullanılabilir limit >= tutar?
│ Checker     │  Redis'ten okur, atomic decrement
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Pre-Auth    │  Hold yaz → Limit düş → Event yayınla
│ Writer      │
└──────┬──────┘
       │
       ▼
ISO 8583 Response (Approve: 00 / Decline: 51,54,57...)
```

**Kritik tasarım kararı:** Limit kontrolü ve pre-auth yazma **atomic** olmalı. Redis'te Lua script ile yapılır:

```lua
-- Redis Lua: Atomic limit check + decrement
local available = tonumber(redis.call('GET', KEYS[1]))
local amount = tonumber(ARGV[1])
if available >= amount then
    redis.call('DECRBY', KEYS[1], amount)
    return 1  -- Approved
else
    return 0  -- Declined: insufficient limit
end
```

#### 4.4.2 Ödeme Sistemleri ve Ağ Geçitleri Entegrasyonu

*   **BKM Switch & ISO 8583 Protokolü:** Gelen provizyon ve yetkilendirme istekleri TCP/IP soketleri üzerinden ISO 8583 standartlarında ayrıştırılır (parsing).
    *   `Field 2 (Primary Account Number - PAN)`: 16-19 haneli müşteri kart numarası.
    *   `Field 3 (Processing Code)`: 6 haneli işlem yönlendirme kodu (örn. Alışveriş, Nakit Avans, İptal).
    *   `Field 4 (Transaction Amount)`: Kuruş bazında noktasız 12 haneli sayısal veri (örn. 17,50 TL tutarındaki bir işlem sisteme `000000001750` olarak yansır).
    *   `Field 38 (Authorization ID Response)`: İşlem başarılı olduğunda bankamız tarafından üretilen 6 haneli provizyon onay kodudur.
*   **TCMB FAST & ISO 20022 Entegrasyonu:**
    *   FAST ağı üzerinden anlık para transferleri için XML tabanlı **`pacs.008` (FIToFICustomerCreditTransfer)** mesaj tipi üretilir ve çözümlenir.
    *   FAST ağından gelen **T01 (Teyit)** ve **T02 (Sonuç Bildirim)** onay/ret mesajları gerçek zamanlı (milisaniyeler içinde) doğrulanır ve hesap bakiyelerine yansıtılır.
    *   **FAST Karekod (QR Code):** Mobil uygulama üzerinden "Karekod Akış Türü 01, 02 ve 03" kodlarına sahip dinamik karekodlar üretilir ve gelen karekod talepleri kriptografik Hash doğrulamasıyla doğrulanıp anlık olarak FAST işlemine dönüştürülür.

---

### 4.5 Transaction Service (Ledger)

**Sorumluluk:** Çift taraflı muhasebe kaydı, işlem geçmişi, settlement

**Teknoloji:** ASP.NET Core 8, Marten (Event Sourcing üzerinde PostgreSQL), Dapper (reporting)

**Double-Entry Bookkeeping ve Ledger Kuralları:**
Her işlem için en az iki kayıt (posting) oluşturulur ve toplam her zaman sıfırlanır. BDDK uyumluluğu ve veri bütünlüğü için şu mimari standartlar uygulanır:

*   **Bakiye Karakteristiği (Asset vs. Liability):**
    *   Müşterilerin mevduat hesapları bankanın yükümlülüğüdür. Bu nedenle müşteri mevduat hesapları **Kredi Normal (Credit Normal - Liability)** hesaplardır.
    *   Bankanın kasasındaki nakit veya TCMB nezdindeki fonları bankanın varlığıdır. Dolayısıyla varlık hesapları **Borç Normal (Debit Normal - Asset)** hesaplardır.
    *   *Para Yatırma Akışı:* Bankanın kasa hesabı borçlandırılırken (Debit +), müşterinin mevduat hesabı alacaklandırılır (Credit +).
*   **Değiştirilemez Denetim İzi (Immutability):** Finansal kayıt tabloları (`postings`) kesinlikle **sadece eklenebilir (append-only)** olmalıdır. Veritabanı seviyesinde `UPDATE` ve `DELETE` işlemleri yasaklanmıştır. Hatalı işlemler ancak ters yönlü bir telafi (reversal) kaydı atılarak düzeltilebilir.
*   **Fixed-Point Aritmetik:** Float yuvarlama hatalarının oluşmasını engellemek için parasal değerler kuruş bazında **tamsayı (integer)** veya yüksek hassasiyetli decimal olarak saklanır.
*   **TigerBeetle Entegrasyonu:** Yüksek TPS (High Transactions Per Second) gerektiren ve çift taraflı kayıt muhasebesini veritabanı düzeyinde zorunlu kılan özel ledger motorları (örn. TigerBeetle) transaction tabanında konumlandırılabilir.

```
Örnek: Müşteri 1.000 TL harcadı (Kuruş cinsinden 100000)

DEBIT   │ Card Receivable Account (Asset)      │ 1.000 TL
CREDIT  │ Customer Liability Acct (Liability)  │ 1.000 TL

Merchant'a ödeme yapıldığında:
DEBIT   │ Customer Liability Acct (Liability)  │ 1.000 TL
CREDIT  │ Nostro / Settlement Acct (Asset)     │ 1.000 TL
```

#### 4.5.2 Dağıtık İşlem Yönetimi ve Saga Deseni (Orchestration Saga)

Farklı veritabanlarını yöneten mikroservisler (Hesap Servisi, Limit Servisi, Ücretlendirme vb.) arasında dağıtık ACID güvencesi sağlamak amacıyla **Orkestrasyon Tabanlı Saga Deseni (Orchestration-based Saga)** kullanılır:

*   **Saga Orchestrator:** Sürecin başlangıcından bitişine kadar durum makinesini (State Machine) yönetir ve servislere gRPC komutları gönderir.
*   **Tazmin Edici İşlemler (Compensating Transactions):** Adımlardan biri (örn. limit düşme veya kara liste kontrolü) başarısız olursa, orkestratör başarısızlığı algılar ve önceki başarılı adımları tersine çevirecek "Compensating" işlemlerini sırayla tetikleyerek veritabanlarını tutarlı ilk hallerine döndürür (Rollback).

> **Choreography mı Orchestration mı? — İkisi birlikte kullanılır.** Bu sistemde desen seçimi akışın doğasına göre yapılır:
> - **Choreography (Kafka event):** Bildirim gönderimi, read-model/projeksiyon güncelleme, raporlama gibi *operasyonel ve gevşek bağlı yayınlar*. Servisler birbirini tanımadan event üzerinden tepki verir.
> - **Orchestration Saga (yukarıdaki):** Çok servisli ve güçlü tutarlılık gerektiren *finansal akışlar* — para transferi, taksitli satış, chargeback/dispute, refund. Bu akışlarda telafi (compensation) sırası ve durum makinesi merkezî bir koordinatör tarafından garanti edilmek zorundadır.

**Transaction Lifecycle (State Machine):**
```
INITIATED
    │
    ▼
AUTHORIZED   ← pre-auth hold aktif
    │
    ├──► REVERSED    (void/cancel — hold serbest bırakılır)
    │
    ▼
CLEARED      ← merchant batch dosyası geldi
    │
    ▼
SETTLED      ← para fiziksel olarak hareket etti
    │
    ├──► DISPUTED    ← chargeback başlatıldı
    │         │
    │         ▼
    │    CHARGEBACK_WON / CHARGEBACK_LOST
    │
    ▼
REFUNDED     ← iade yapıldı
```

---

### 4.6 Installment Service

**Sorumluluk:** Taksit planı hesaplama, taksit takibi, kampanya yönetimi

**Teknoloji:** ASP.NET Core 8, EF Core, PostgreSQL

**Taksit Hesaplama Motoru:**

```csharp
public class InstallmentCalculator
{
    // Türk bankacılığında standart taksit formülü
    public InstallmentPlan Calculate(InstallmentRequest request)
    {
        decimal principal = request.Amount;
        int count = request.InstallmentCount;
        decimal monthlyRate = request.MonthlyInterestRate;

        // Eşit taksit (Annüite) formülü
        decimal monthlyPayment = principal *
            (monthlyRate * Math.Pow(1 + monthlyRate, count)) /
            (Math.Pow(1 + monthlyRate, count) - 1);

        decimal totalAmount = monthlyPayment * count;
        decimal totalInterest = totalAmount - principal;

        // KKDF: Tüketici kredisinde %15 (2024 oranı)
        decimal kkdf = totalInterest * 0.15m;

        // BSMV: %5
        decimal bsmv = totalInterest * 0.05m;

        return BuildPlan(monthlyPayment, totalInterest, kkdf, bsmv, count);
    }
}
```

**Kampanya Tipleri:**
```
- StandardInstallment    : Normal faizli taksit
- DeferredInstallment    : İlk N ay ödemesiz, sonra başlar
- ZeroInterest           : Üye işyeri faizi karşılar (MDR'dan düşülür)
- ExtraInstallment       : Kampanya döneminde +N taksit
- BonusInstallment       : Kart bonusu ile taksit
```

---

### 4.7 Fraud Service

**Sorumluluk:** Fraud kural motoru, real-time ve batch fraud analizi

**Teknoloji:** ASP.NET Core 8, Redis (velocity), PostgreSQL (rules & cases)

**Rule Engine Mimarisi:**

```csharp
public interface IFraudRule
{
    string RuleCode { get; }
    int Priority { get; }
    Task<RuleResult> EvaluateAsync(AuthorizationContext context);
}

// Örnek kurallar:
// VelocityRule          : 1 saatte 10'dan fazla işlem → decline
// GeographicRule        : 2 farklı ülkede 30dk içinde işlem → block
// AmountAnomalyRule     : Ortalama harcamanın 5x üstü → 3DS zorla
// BlacklistRule         : Kara listede merchant/BIN → decline
// NightTimeRule         : Gece 02-05 arası nakit avans → OTP iste
```

**Karar Matrisi:**
```
Score 0-30   → APPROVE
Score 31-60  → APPROVE with 3DS challenge
Score 61-85  → APPROVE with OTP
Score 86-100 → DECLINE + case açılır
```

---

### 4.8 EOD Service (End of Day)

**Sorumluluk:** Günlük batch işlemler, faiz hesaplama, ekstre üretimi

*(Detay: bkz. Bölüm 10)*

---

### 4.9 Reconciliation Service

**Sorumluluk:** İç mutabakat, BKM/Visa/MC dosya mutabakatı

**Günlük akış:**
```
1. Scheme clearing dosyası gelir (VISA NET, MC IPM formatı)
2. İç transaction kayıtlarıyla eşleştirilir
3. Farklar (exception) işaretlenir
4. Otomatik çözümlenebilir farklar kapanır
5. Manuel inceleme gereken farklar case açılır
6. Mutabakat raporu üretilir
```

**Exception tipleri:**
```
AMOUNT_MISMATCH       : Tutar uyuşmuyor
MISSING_IN_SCHEME     : İç kayıt var, scheme dosyasında yok
MISSING_IN_INTERNAL   : Scheme dosyasında var, iç kayıt yok
DUPLICATE             : Aynı işlem iki kez gelmiş
CURRENCY_MISMATCH     : Para birimi farklı
```

---

### 4.10 Notification Service

**Sorumluluk:** SMS, e-posta, push bildirim yönetimi

**Teknoloji:** ASP.NET Core 8, MassTransit + Kafka, Hangfire (retry)

**Event'e göre tetiklenen bildirimler:**
```
TransactionAuthorized  → SMS: "1.000 TL harcama yapıldı"
CardBlocked            → SMS + Push: "Kartınız bloke edildi"
StatementGenerated     → E-posta: "Ekstreniz hazır"
InstallmentDue         → Push: "Taksit vadesi yaklaşıyor"
FraudDetected          → SMS + E-posta: "Şüpheli işlem tespit edildi"
```

---

## 5. Veritabanı Mimarisi

### 5.1 Account DB Schema

```sql
-- Müşteri
CREATE TABLE customers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    national_id     VARCHAR(11) NOT NULL UNIQUE,  -- TC Kimlik No (encrypted)
    first_name      VARCHAR(100) NOT NULL,
    last_name       VARCHAR(100) NOT NULL,
    birth_date      DATE NOT NULL,
    email           VARCHAR(255),
    phone           VARCHAR(20),
    kyc_status      VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    risk_segment    VARCHAR(20),                  -- LOW, MEDIUM, HIGH
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    version         INTEGER NOT NULL DEFAULT 1    -- Optimistic locking
);

-- Hesap
CREATE TABLE accounts (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id         UUID NOT NULL REFERENCES customers(id),
    account_number      VARCHAR(26) NOT NULL UNIQUE,  -- IBAN formatı
    account_type        VARCHAR(20) NOT NULL,          -- CURRENT, SAVINGS, CREDIT_CARD
    currency            CHAR(3) NOT NULL DEFAULT 'TRY',
    status              VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    balance             NUMERIC(19,4) NOT NULL DEFAULT 0,
    available_balance   NUMERIC(19,4) NOT NULL DEFAULT 0,
    hold_amount         NUMERIC(19,4) NOT NULL DEFAULT 0,
    opened_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    closed_at           TIMESTAMPTZ,
    version             INTEGER NOT NULL DEFAULT 1
);

-- Hold (Pre-auth blokajları)
CREATE TABLE account_holds (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      UUID NOT NULL REFERENCES accounts(id),
    authorization_id UUID NOT NULL,
    amount          NUMERIC(19,4) NOT NULL,
    currency        CHAR(3) NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,    -- 30 gün sonra otomatik serbest
    released_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index stratejisi
CREATE INDEX idx_accounts_customer_id ON accounts(customer_id);
CREATE INDEX idx_accounts_account_number ON accounts(account_number);
CREATE INDEX idx_holds_account_id_active ON account_holds(account_id)
    WHERE released_at IS NULL;
```

### 5.2 Card DB Schema

```sql
-- BIN Tablosu (BKM/Visa/MC'den periyodik güncellenir)
CREATE TABLE bin_table (
    bin             VARCHAR(8) PRIMARY KEY,
    scheme          VARCHAR(20) NOT NULL,     -- VISA, MASTERCARD, TROY
    card_type       VARCHAR(20) NOT NULL,     -- CREDIT, DEBIT, PREPAID
    issuer_name     VARCHAR(100),
    country_code    CHAR(3),
    is_3ds_enabled  BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Kart
CREATE TABLE cards (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      UUID NOT NULL,
    bin             VARCHAR(8) NOT NULL REFERENCES bin_table(bin),
    last_four       CHAR(4) NOT NULL,
    pan_hash        VARCHAR(64) NOT NULL UNIQUE,   -- SHA-256(PAN)
    pan_token       VARCHAR(64) NOT NULL UNIQUE,   -- Vault'taki token
    expiry_month    SMALLINT NOT NULL,
    expiry_year     SMALLINT NOT NULL,
    card_type       VARCHAR(20) NOT NULL,
    scheme          VARCHAR(20) NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'INACTIVE',
    embossed_name   VARCHAR(50) NOT NULL,
    is_virtual      BOOLEAN NOT NULL DEFAULT FALSE,
    issued_at       TIMESTAMPTZ,
    activated_at    TIMESTAMPTZ,
    cancelled_at    TIMESTAMPTZ,
    version         INTEGER NOT NULL DEFAULT 1
);

-- Kart Limitleri
CREATE TABLE card_limits (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    card_id                     UUID NOT NULL REFERENCES cards(id),
    spending_limit              NUMERIC(19,4) NOT NULL,
    available_spending_limit    NUMERIC(19,4) NOT NULL,
    cash_advance_limit          NUMERIC(19,4) NOT NULL,
    available_cash_limit        NUMERIC(19,4) NOT NULL,
    installment_limit           NUMERIC(19,4) NOT NULL,
    abroad_limit                NUMERIC(19,4),
    single_transaction_limit    NUMERIC(19,4),
    effective_from              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    effective_to                TIMESTAMPTZ,      -- NULL = geçerli limit
    is_temporary                BOOLEAN NOT NULL DEFAULT FALSE
);

-- Kart Statü Geçiş Geçmişi (Audit)
CREATE TABLE card_status_history (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    card_id         UUID NOT NULL REFERENCES cards(id),
    from_status     VARCHAR(20),
    to_status       VARCHAR(20) NOT NULL,
    reason          VARCHAR(100),
    changed_by      UUID,                     -- Operatör veya sistem
    changed_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 5.3 Transaction DB Schema (Event Sourced)

```sql
-- Event Store (Marten ile yönetilir, bu şema otomatik oluşturulur)
CREATE TABLE mt_events (
    seq_id          BIGSERIAL PRIMARY KEY,
    id              UUID NOT NULL UNIQUE,
    stream_id       UUID NOT NULL,
    stream_type     VARCHAR(200) NOT NULL,
    version         INTEGER NOT NULL,
    type            VARCHAR(200) NOT NULL,
    data            JSONB NOT NULL,
    metadata        JSONB,
    timestamp       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    tenant_id       VARCHAR(100)
);

CREATE UNIQUE INDEX idx_events_stream_version ON mt_events(stream_id, version);

-- Read Model (Projection — event'lerden türetilir)
CREATE TABLE transaction_projections (
    id                  UUID PRIMARY KEY,
    account_id          UUID NOT NULL,
    card_id             UUID,
    reference_number    VARCHAR(50) NOT NULL UNIQUE,
    type                VARCHAR(30) NOT NULL,    -- PURCHASE, REFUND, CASH, FEE
    status              VARCHAR(20) NOT NULL,
    amount              NUMERIC(19,4) NOT NULL,
    currency            CHAR(3) NOT NULL,
    merchant_name       VARCHAR(200),
    merchant_category   VARCHAR(10),             -- MCC kodu
    description         VARCHAR(500),
    authorized_at       TIMESTAMPTZ,
    cleared_at          TIMESTAMPTZ,
    settled_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL
);

-- Partition by month (büyük hacimler için)
CREATE TABLE transaction_projections_2024_01
    PARTITION OF transaction_projections
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

-- Ledger (Çift Taraflı Muhasebe)
CREATE TABLE ledger_entries (
    id              BIGSERIAL PRIMARY KEY,
    transaction_id  UUID NOT NULL,
    entry_type      CHAR(2) NOT NULL,            -- DR (Debit) / CR (Credit)
    account_code    VARCHAR(20) NOT NULL,
    amount          NUMERIC(19,4) NOT NULL,
    currency        CHAR(3) NOT NULL,
    value_date      DATE NOT NULL,
    description     VARCHAR(500),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Her transaction_id için DR toplamı = CR toplamı constraint
-- (Trigger ile sağlanır)
```

### 5.4 EOD DB Schema

```sql
-- EOD İş Takibi
CREATE TABLE eod_jobs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    business_date   DATE NOT NULL UNIQUE,
    status          VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    error_message   TEXT
);

-- EOD Adım Takibi
CREATE TABLE eod_steps (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    eod_job_id      UUID NOT NULL REFERENCES eod_jobs(id),
    step_name       VARCHAR(100) NOT NULL,
    step_order      INTEGER NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    records_processed INTEGER,
    error_message   TEXT
);

-- Faiz Hesaplama Sonuçları
CREATE TABLE interest_calculations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      UUID NOT NULL,
    calculation_date DATE NOT NULL,
    daily_balance   NUMERIC(19,4) NOT NULL,
    daily_rate      NUMERIC(10,8) NOT NULL,
    accrued_interest NUMERIC(19,4) NOT NULL,
    is_posted       BOOLEAN NOT NULL DEFAULT FALSE
);
```

---

## 6. Event Kataloğu (Kafka)

### Topic Yapısı

```
Naming Convention: minibank.{domain}.{entity}.{action}
Partition Key:     İlgili entity ID (card_id, account_id, vb.)
Retention:         7 gün (operasyonel), 90 gün (finansal)
Replication:       3 (production)
```

### Event Listesi

```yaml
# ACCOUNT EVENTS
Topic: minibank.account.account.created
Topic: minibank.account.account.status-changed
Topic: minibank.account.balance.updated
Topic: minibank.account.hold.placed
Topic: minibank.account.hold.released

# CARD EVENTS
Topic: minibank.card.card.issued
Topic: minibank.card.card.activated
Topic: minibank.card.card.status-changed
Topic: minibank.card.limit.updated
Topic: minibank.card.limit.exceeded       # Fraud için

# AUTHORIZATION EVENTS
Topic: minibank.authorization.auth.approved
Topic: minibank.authorization.auth.declined
Topic: minibank.authorization.auth.reversed
Topic: minibank.authorization.preauth.expired

# TRANSACTION EVENTS
Topic: minibank.transaction.txn.created
Topic: minibank.transaction.txn.cleared
Topic: minibank.transaction.txn.settled
Topic: minibank.transaction.txn.refunded
Topic: minibank.transaction.txn.disputed
Topic: minibank.transaction.chargeback.opened
Topic: minibank.transaction.chargeback.resolved

# INSTALLMENT EVENTS
Topic: minibank.installment.plan.created
Topic: minibank.installment.installment.due
Topic: minibank.installment.installment.paid
Topic: minibank.installment.plan.cancelled

# FRAUD EVENTS
Topic: minibank.fraud.case.opened
Topic: minibank.fraud.case.resolved
Topic: minibank.fraud.card.blocked-by-fraud

# EOD EVENTS
Topic: minibank.eod.job.started
Topic: minibank.eod.job.completed
Topic: minibank.eod.statement.generated
Topic: minibank.eod.interest.accrued
```

### Event Şema Örneği

```json
// minibank.authorization.auth.approved
{
  "eventId": "evt_01HX4K...",
  "eventType": "AuthorizationApproved",
  "eventVersion": "1.0",
  "occurredAt": "2024-01-15T14:23:11.456Z",
  "correlationId": "corr_01HX...",
  "payload": {
    "authorizationId": "auth_01HX...",
    "cardId": "card_01HX...",
    "accountId": "acc_01HX...",
    "amount": 1500.00,
    "currency": "TRY",
    "merchantName": "MIGROS",
    "merchantCategoryCode": "5411",
    "approvalCode": "123456",
    "responseCode": "00",
    "installmentCount": 3,
    "isInternational": false
  }
}
```

---

## 7. API Tasarımı

### 7.1 REST Standartları

```
Base URL:    https://api.minibank.io/v1
Auth:        Bearer JWT (Header: Authorization)
Format:      JSON (Content-Type: application/json)
Versioning:  URL path (/v1, /v2)
Pagination:  Cursor-based (finansal kayıtlar için)
```

### 7.2 Endpoint Örnekleri

```http
# ACCOUNT
GET    /v1/accounts/{id}
GET    /v1/accounts/{id}/balance
GET    /v1/accounts/{id}/transactions?cursor=&limit=50
POST   /v1/accounts

# CARD
GET    /v1/cards/{id}
GET    /v1/cards/{id}/limits
PUT    /v1/cards/{id}/status          # { "status": "BLOCKED", "reason": "LOST" }
PUT    /v1/cards/{id}/limits/temporary
POST   /v1/cards

# AUTHORIZATION (internal — POS entegrasyonu için)
POST   /v1/authorizations
DELETE /v1/authorizations/{id}        # Reversal

# TRANSACTION
GET    /v1/transactions/{id}
POST   /v1/transactions/{id}/dispute
POST   /v1/transactions/{id}/refund

# INSTALLMENT
POST   /v1/installments/calculate     # Plan hesaplama (işlem öncesi)
GET    /v1/installments/{planId}
POST   /v1/installments/{planId}/cancel

# STATEMENT
GET    /v1/statements?year=2024&month=1
GET    /v1/statements/{id}/download   # PDF
```

### 7.3 Hata Formatı (RFC 7807 Problem Details)

```json
{
  "type": "https://api.minibank.io/errors/insufficient-limit",
  "title": "Insufficient Card Limit",
  "status": 422,
  "detail": "Available spending limit (850.00 TRY) is less than requested amount (1000.00 TRY)",
  "instance": "/v1/authorizations/auth_01HX...",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "extensions": {
    "availableLimit": 850.00,
    "requestedAmount": 1000.00,
    "currency": "TRY",
    "declineCode": "51"
  }
}
```

### 7.4 Idempotency

Tüm POST endpoint'leri `Idempotency-Key` header'ı alır:

```http
POST /v1/authorizations
Idempotency-Key: idem_01HX4K9QMKJX8ZVTXEF...

# Aynı key ile tekrar gelen istek cached response döner (Redis, 24 saat TTL)
```

---

## 8. Güvenlik Mimarisi

### 8.1 Katmanlı Güvenlik Modeli (Sıfır Güven - Zero Trust)

Banka ağında çevre güvenliği (perimeter security) yaklaşımı yerine her servis geçişinin doğrulandığı **Sıfır Güven (Zero Trust)** yaklaşımı benimsenmiştir:
*   **Layer 1: Network:** TLS 1.3 (iç ağda mTLS), IP Whitelist, DDoS koruması (Cloudflare/Fortinet) ve mikro ağ segmentasyonu.
*   **Layer 2: API Gateway:** Rate Limiting (istek sınırlama), WAF (Web Uygulama Güvenlik Duvarı), Bot tespiti ve OAuth 2.0 token introspect kontrolleri.
*   **Layer 3: Application:** JWT imza doğrulaması, rol tabanlı erişim kontrolü (RBAC) ve yetkilendirme kapsamları (scopes).
*   **Layer 4: Data:** Duran verilerin şifrelenmesi (TDE), dinamik veri maskeleme (DDM) ve FIPS 140-2 Level 3 HSM ile anahtar yönetimi.
*   **Layer 5: Audit:** 5651 Sayılı Kanun ve BDDK uyumlu değiştirilemez (WORM) log ve denetim izleri.

### 8.2 Kimlik Doğrulama ve Yetkilendirme Politikaları

*   **Güçlü Kimlik Doğrulama (MFA):** Kullanıcı şifreleri sunucu tarafında **PBKDF2** veya **Argon2** algoritmaları kullanılarak tuzlanmış (salted) şekilde hash'lenir. Girişlerde şifreye ek olarak tek kullanımlık şifre (OTP - SMS/Mobil Onay) veya biyometrik doğrulama zorunludur.
*   **e-Devlet Entegrasyonu (SSO):** Müşteri kazanım ve internet şubesi girişlerinde e-Devlet Kapısı OAuth (SSO) API'leri ile entegrasyon sağlanır. Kullanıcılar e-Devlet kimlikleriyle doğrudan banka şubesine geçiş yapabildikleri gibi, banka şubesinden de e-Devlet'e güvenli kimlik transferi gerçekleştirebilirler.
*   **OAuth2 / OpenID Connect:** Mobil ve web kanalları standart OpenID Connect ile token tabanlı doğrulanır. Üçüncü Taraf Sağlayıcılar (YÖS) mTLS ve FAPI uyumlu token altyapısıyla yetkilendirilir.

### 8.3 Kart Verisi Güvenliği (PCI-DSS Uyum)

```
PAN (Kart Numarası):
  - Asla plaintext saklanmaz
  - DB'de: pan_token (Vault referansı) + pan_hash (arama için)
  - Logda: Asla görünmez (log scrubber middleware)
  - API response'da: Maskelenmiş (**** **** **** 1234)

CVV2:
  - Asla saklanmaz (ne DB'de, ne logda)
  - Sadece authorization anında doğrulanır, sonra silinir

PAN Token Flow:
  PAN → [Vault Service] → Token
  Token → DB'ye saklanır
  Ödeme anında: Token → [Vault] → PAN → Scheme'e gönderilir
```

### 8.4 Encryption & Donanımsal Şifreleme (HSM)

```
At Rest:     AES-256-GCM (PostgreSQL TDE veya kolon seviyesi şifreleme)
In Transit:  mTLS / TLS 1.3 (tüm iç ve dış ağlarda zorunlu)
Field Level: TC Kimlik No, IBAN, Telefon, Adres, Biyometrik veriler -> AES-256 şifreli kolonlar
Key Mgmt:    FIPS 140-2 Level 3 HSM (Donanımsal Güvenlik Modülü). Anahtarlar HSM dışına sızdırılamaz.
Zeroization: HSM cihazlarına fiziksel müdahale (tamper) algılandığında şifreleme anahtarları anında imha edilir.
```

### 8.5 Rate Limiting & Güvenlik Testleri

```
/v1/authorizations   → 100 req/min per merchant
/v1/auth/login       → 5 req/min per IP
/v1/*                → 1000 req/min per API key
```

Siber dayanıklılığı sürdürmek için Statik Kod Analizi (SAST - SonarQube/Fortify), Dinamik Güvenlik Taramaları (DAST) ve yılda en az bir kez bağımsız yetkili firmalarca sızma (penetrasyon) testleri yapılması zorunludur. PCI DSS ve ISO 27001 sertifikasyonları yasal denetimlerin temelidir.

---

## 9. Ödeme Akışları

### 9.1 Satın Alma Akışı (End-to-End)

```
Müşteri           POS / SaPoS          Authorization        Card         Limit        Fraud        Transaction
   │                   │                   Service           Service      (Redis)      Service       Service
   │  Kart taktı       │                       │                │            │            │              │
   │──────────────────►│                       │                │            │            │              │
   │                   │  ISO 8583 Auth Req    │                │            │            │              │
   │                   │──────────────────────►│                │            │            │              │
   │                   │                       │  GetCard()     │            │            │              │
   │                   │                       │───────────────►│            │            │              │
   │                   │                       │◄───────────────│            │            │              │
   │                   │                       │  CheckLimit()  │            │            │              │
   │                   │                       │───────────────────────────►│            │              │
   │                   │                       │◄───────────────────────────│            │              │
   │                   │                       │  FraudPreCheck()            │            │              │
   │                   │                       │──────────────────────────────────────►│              │
   │                   │                       │◄──────────────────────────────────────│              │
   │                   │                       │  DecrementLimit() [atomic]  │            │              │
   │                   │                       │───────────────────────────►│            │              │
   │                   │                       │  PublishEvent(AuthApproved) │            │              │
   │                   │                       │──────────────────────────────────────────────────────►│
   │                   │  ISO 8583 Auth Resp   │                │            │            │              │
   │                   │◄──────────────────────│                │            │            │              │
   │  Onaylandı        │                       │                │            │            │              │
   │◄──────────────────│                       │                │            │            │              │
```

### 9.2 Taksitli İşlem Akışı

```
1. Authorization: Toplam tutar bloke edilir (1.000 TL x 3 taksit = 3.000 TL)
2. Clearing gelir: InstallmentService plan oluşturur
3. EOD her ay: Taksit vadesi geldiğinde hesaptan düşülür
4. Ledger: Her taksit için ayrı ledger entry
```

### 9.3 İptal ve İade (Reversal vs Refund)

```
Reversal (Void):
  - Clearing gelmeden önce
  - Pre-auth hold anında serbest bırakılır
  - Ledger kaydı oluşmaz
  - Aynı gün yapılabilir

Refund:
  - Clearing sonrası, para merchant'a geçtikten sonra
  - Yeni bir ters işlem oluşturulur
  - Ledger'a ters kayıt düşer
  - 1-5 iş günü sürebilir
```

---

## 10. EOD / Batch Mimarisi

### 10.1 EOD Job Sıralaması

```
21:00  Pre-EOD Snapshot    → Anlık bakiye ve limit snapshot'ı
21:30  EOD Lock            → Yeni işlem kabulü durdurulur (sadece okuma)
21:35  Hold Expiry Check   → 30 günü geçen hold'lar serbest bırakılır
21:40  Interest Accrual    → Günlük faiz birikimi hesaplanır
22:00  Statement Cut       → Ekstre kesim günü olan hesaplara ekstre üretilir
22:30  Minimum Payment Calc→ Asgari ödeme tutarı hesaplanır
22:45  Late Fee Calculation → Vadesi geçmiş hesaplara gecikme faizi
23:00  Installment Charge  → Vadesi gelen taksitler tahsil edilir
23:15  BKM Reconciliation  → BKM'nin clearing dosyasıyla mutabakat
23:30  Limit Refresh       → Ödeme yapan hesapların limiti açılır
23:45  Report Generation   → BDDK raporları, iç raporlar
00:00  BOD Start           → Yeni gün başlar, EOD Lock kalkar
```

### 10.2 Quartz.NET Job Örneği

```csharp
[DisallowConcurrentExecution]
public class InterestAccrualJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var businessDate = (DateOnly)context.JobDetail.JobDataMap["businessDate"];

        // Tüm kredi kartı hesaplarını chunk'lar halinde işle
        await foreach (var batch in _accountRepo.GetCreditAccountsBatchAsync(batchSize: 1000))
        {
            var calculations = batch.Select(account =>
                _interestCalculator.Calculate(account, businessDate));

            await _interestRepo.BulkInsertAsync(calculations);
            await _eventBus.PublishBatchAsync(calculations.Select(c =>
                new InterestAccruedEvent(c.AccountId, c.AccruedInterest, businessDate)));
        }
    }
}
```

### 10.3 Başarısız EOD Stratejisi

```
Adım başarısız olursa:
1. Adım retry edilir (max 3 kez, 5 dk arayla)
2. Hâlâ başarısız → EOD kısmen tamamlanmış state'e girer
3. Alert tetiklenir (PagerDuty/OpsGenie)
4. Operatör manuel müdahale yapar
5. Başarısız adımdan devam edilebilir (checkpoint)

Rollback:
- Finansal kayıtlar event sourced → geriye sarılamaz
- Bunun yerine "compensating transaction" yazılır
- Örnek: Yanlış faiz hesaplandı → ters faiz kaydı oluşturulur
```

---

## 11. Resilience Stratejisi

### 11.1 Her Servis İçin Pattern Seti

```csharp
// Polly ile resilience pipeline
services.AddResiliencePipeline("payment-critical", builder =>
{
    builder
        // Timeout: 200ms (authorization için)
        .AddTimeout(TimeSpan.FromMilliseconds(200))

        // Retry: 3 kez, exponential backoff (sadece geçici hatalar)
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(50),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
            // NOT: Insufficient limit gibi business hataları retry edilmez!
        })

        // Circuit Breaker: 5 saniyede 50% hata → devre açılır
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(5),
            BreakDuration = TimeSpan.FromSeconds(30)
        });
});
```

### 11.2 Fallback Stratejileri

```
Authorization Service Redis'e ulaşamıyor:
  → DB'den limit oku (yavaş ama doğru)
  → P99 hedefi tutmazsa: stand-in processing (offline auth)

Fraud Service yanıt vermiyor:
  → Varsayılan kural seti uygulanır (sadece blacklist check)
  → Case açılır, sonradan review edilir

Kafka publish başarısız:
  → Outbox Pattern: DB'ye yaz, ayrı worker Kafka'ya gönderir
  → At-least-once delivery garanti altında
```

### 11.3 Outbox Pattern

```csharp
// Transaction ile birlikte aynı DB transaction'ında outbox kaydı açılır
public async Task AuthorizeAsync(AuthorizationCommand command)
{
    await using var tx = await _db.BeginTransactionAsync();

    var result = await ProcessAuthorizationAsync(command);
    
    // Outbox: Event mutlaka gönderilecek
    await _outboxRepo.AddAsync(new OutboxMessage
    {
        EventType = "AuthorizationApproved",
        Payload = JsonSerializer.Serialize(result),
        CreatedAt = DateTime.UtcNow
    });

    await tx.CommitAsync();
    // Worker ayrı process'te outbox'ı okuyup Kafka'ya gönderir
}
```

### 11.4 Felaket Kurtarma (Disaster Recovery) ve RTO/RPO Hedefleri

Banka bilgi sistemlerinde iş sürekliliğini garanti altına almak ve regülatif denetim standartlarını karşılamak amacıyla aşağıdaki RTO ve RPO matrisi ve felaket kurtarma stratejisi uygulanmaktadır:

| Felaket Senaryosu | RTO (Hedeflenen Kurtarma Süresi) | RPO (Hedeflenen Veri Kaybı) | Felaket Kurtarma (DR) Stratejisi |
| :--- | :--- | :--- | :--- |
| **Donanım / Yazılım Arızası** *(Tekil sunucu veya pod çökmesi)* | Dakika Düzeyi (~5 dk) | 0 *(Veri Kaybı Yok)* | **Yüksek Erişilebilirlik (HA) & Hızlı Failover:** Yedek podlar diğer kullanılabilirlik bölgelerinde (Multi-AZ) hazır bekler. PostgreSQL veri tabanlarında Patroni HA ile otomatik master değişimi yapılır. |
| **Bölgesel Afet / Veri Merkezi Arızası** *(Birincil veri merkezi kaybı)* | 1 Saat | ~5-10 Dakika | **Çoklu Bölge Dağıtımı:** İstanbul (Primary) ve Ankara (DR) veri merkezleri arasında **asenkron** veri replikasyonu. (Bölgeler arası mesafe nedeniyle senkron değildir; bu yüzden RPO sıfır değil, dakikalar düzeyindedir.) İstanbul'da arıza durumunda DNS yönlendirmesiyle Ankara otomatik olarak birincil rolü üstlenir. |
| **Kapsamlı Felaket / Ülkesel Altyapı Kesintisi** | 6-12 Saat | ~1 Saat | **Offsite Yedekleme & Soğuk/Ilık Kurtarma:** Kritik veritabanları, eklenebilir işlem kayıtları (Core Ledger) ve şifreleme anahtarları, 3-2-1-1 yedekleme kuralı çerçevesinde donanımsal kilitli **WORM (Write-Once-Read-Many)** depolama alanlarından geri yüklenir. |

*Felaket kurtarma tatbikatları mevzuat gereği yılda en az bir kez tüm banka genelinde başarıyla gerçekleştirilmek zorundadır.*

---

## 12. Gözlemlenebilirlik (Observability)

### 12.1 The Three Pillars

```
LOGS    → Serilog → Elasticsearch → Kibana
METRICS → OpenTelemetry → Prometheus → Grafana
TRACES  → OpenTelemetry → Jaeger
```

### 12.2 Kritik Metrikler

```
# Authorization
authorization_total{result="approved|declined"} counter
authorization_duration_ms histogram (P50, P95, P99)
authorization_decline_rate gauge (>5% = alarm)

# Limit
limit_check_redis_hit_ratio gauge (<95% = alarm)
available_limit_exhausted_count counter

# EOD
eod_job_duration_seconds histogram
eod_step_failed_total counter
interest_accrual_total_amount gauge

# Fraud
fraud_rule_triggered_total{rule_code="..."} counter
fraud_score_distribution histogram
```

### 12.3 Distributed Trace Örneği

```
Trace: POST /v1/authorizations  [total: 87ms]
│
├── IAM: JWT Validation          [2ms]
├── Card Service: GetCard        [8ms]
│   └── DB Query: cards          [5ms]
├── Redis: LimitCheck            [1ms]
├── Fraud Service: PreCheck      [12ms]
│   └── Redis: VelocityCheck     [2ms]
│   └── DB: BlacklistCheck       [4ms]
├── Redis: DecrementLimit        [1ms]
├── DB: WritePreAuth             [15ms]
└── Kafka: PublishEvent          [3ms]
```

---

## 13. Altyapı ve Deployment

### 13.1 Docker Compose (Development)

```yaml
version: '3.9'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - ./init-scripts:/docker-entrypoint-initdb.d
    ports: ["5432:5432"]

  kafka:
    image: confluentinc/cp-kafka:7.6.0
    environment:
      KAFKA_KRAFT_MODE: "true"
      KAFKA_PROCESS_ROLES: "broker,controller"
      KAFKA_NUM_PARTITIONS: 12
    ports: ["9092:9092"]

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass ${REDIS_PASSWORD} --maxmemory 512mb
    ports: ["6379:6379"]

  vault:
    image: hashicorp/vault:1.16
    environment:
      VAULT_DEV_ROOT_TOKEN_ID: ${VAULT_TOKEN}
    ports: ["8200:8200"]

  jaeger:
    image: jaegertracing/all-in-one:1.56
    ports: ["16686:16686", "4317:4317"]

  # Services
  api-gateway:
    build: ./src/ApiGateway
    ports: ["8080:8080"]
    depends_on: [iam-service]

  iam-service:
    build: ./src/IamService
    environment:
      ConnectionStrings__Default: "Host=postgres;Database=iam_db;..."

  account-service:
    build: ./src/AccountService

  card-service:
    build: ./src/CardService

  authorization-service:
    build: ./src/AuthorizationService

  transaction-service:
    build: ./src/TransactionService

  installment-service:
    build: ./src/InstallmentService

  fraud-service:
    build: ./src/FraudService

  eod-service:
    build: ./src/EodService

  reconciliation-service:
    build: ./src/ReconciliationService

  notification-service:
    build: ./src/NotificationService
```

### 13.2 Standart Kubernetes & Co-Location (Production)

Üretim ortamı, Türkiye sınırları içerisinde yer alan, **TSE TS EN 50600 (Sınıf 3)** ve Tier 3 / Tier 4 sertifikalı fiziksel veri merkezlerinde (İstanbul ve Ankara coğrafi konumlarında), bankaya tahsis edilmiş dedicated donanımlar üzerinde kurulu standart **Kubernetes** orkestrasyonu üzerinde çalışmaktadır.

```yaml
# Her mikroservis için Kubernetes nesneleri:
# - Deployment (İstanbul ve Ankara veri merkezleri arasında multi-AZ dağılım)
# - Service (ClusterIP) ve Ingress (mTLS ve API Gateway koruması ile)
# - ConfigMap & HSM/Vault entegrasyonlu Secrets
# - PodDisruptionBudget (her lokasyonda yüksek kesintisizlik)
# - ResourceQuota & LimitRanges (CPU/Memory kısıtları)
# - NetworkPolicy (Microsegmentation ve CDE bölge izolasyonu)
# - HorizontalPodAutoscaler (CPU %70 veya istek/saniye eşikleriyle otomatik ölçekleme)
```

İstanbul ve Ankara arasındaki ağ trafiği, split-brain senaryolarına karşı İzmir veri merkezindeki bir **Witness Node** ile 3-Node Quorum mekanizmasıyla yönetilir. Dış hatların tamamen kesildiği olağanüstü durumlarda, yurt içi ağlar üzerinden sistem en geç **24 saat içinde kesintisiz** çalışmaya devam edebilir.

### 13.3 PostgreSQL Enterprise HA & WORM Yedekleme

Finansal defter ve işlem veritabanı altyapısı aşağıdaki gibi kurgulanmıştır:

```
Production Database Cluster:
  - Patroni HA: Küme yönetimi ve otomatik failover
  - Veri Replikasyonu (Hibrit): Birincil veri merkezi İÇİNDE senkron replika (metro-içi, RPO = 0) | İstanbul (Primary) <-> Ankara (DR) ARASI asenkron replikasyon (RPO ~dakikalar)
  - PgBouncer: Bağlantı havuzlama
  - 3-2-1-1 Yedekleme Stratejisi: 3 kopya veri, 2 farklı ortam, 1 coğrafi uzaklık, 1 WORM (değiştirilemez) donanım kilidi
  - Güvenlik: PostgreSQL TDE, AES-256 kolon bazlı KVKK şifrelemesi
```

### 13.4 Maliyet ve Teknoloji Yığını Seçimi (Açık Kaynak vs. Ticari/Enterprise)

Bankanın maliyet verimliliği ve kritik enterprise destek ihtiyaçları doğrultusunda belirlenen teknoloji yığını alternatifleri aşağıda karşılaştırılmıştır:

| Katman / İşlev | Açık Kaynak Seçimi | Ticari / Enterprise Alternatifi |
| :--- | :--- | :--- |
| **Programlama** | Go, Node.js, Java/Spring | **.NET 8 (C#)**, Enterprise Java (JakartaEE) |
| **Veritabanı** | **PostgreSQL**, MySQL, MongoDB | Oracle DB, MS SQL Server |
| **Mesajlaşma** | **Apache Kafka**, RabbitMQ | IBM MQ, TIBCO EMS |
| **API Gateway** | Kong, Tyk, KrakenD | **YARP**, Apigee, WSO2 |
| **Kubernetes** | Vanilla Kubernetes | **Red Hat OpenShift**, Rancher Enterprise |
| **İzleme & Log** | **ELK/EFK**, **Prometheus/Grafana**, **Jaeger** | Splunk, Datadog, New Relic |
| **CI/CD** | Jenkins, **GitLab CI/CD**, ArgoCD | Azure DevOps, GitHub Enterprise |
| **Güvenlik** | **HashiCorp Vault**, OPA (policy engine) | **Thales HSM / Luna**, AWS KMS |
| **Test & Kaos** | JUnit/xUnit, Chaos Mesh | HP LoadRunner, Qualys |

---

## 14. Geliştirme Yol Haritası

### Faz 1 — Temel (4-6 hafta)
```
✓ Proje scaffolding (solution structure, shared libraries)
✓ IAM Service (JWT, RBAC)
✓ Account Service (CRUD, balance)
✓ Card Service (card lifecycle, limits)
✓ API Gateway (YARP)
✓ Docker Compose kurulumu
✓ Temel test altyapısı (xUnit + Testcontainers)
```

### Faz 2 — Ödeme Çekirdeği (6-8 hafta)
```
✓ Authorization Service (ISO 8583 parser, limit check)
✓ Transaction Service (Event Sourcing, Ledger)
✓ Fraud Service (Rule Engine, Velocity)
✓ Kafka event altyapısı
✓ Outbox Pattern
✓ Idempotency katmanı
```

### Faz 3 — Taksit ve EOD (4-6 hafta)
```
✓ Installment Service (hesaplama motoru, plan yönetimi)
✓ EOD Service (Quartz.NET, tüm batch job'lar)
✓ Reconciliation Service
✓ Statement generation
```

### Faz 4 — Production Readiness (4 hafta)
```
✓ Observability (OpenTelemetry, Prometheus, Grafana, Jaeger)
✓ Resilience (Polly, Circuit Breaker)
✓ Security hardening (Vault, PCI-DSS kontroller)
✓ Kubernetes manifests
✓ Load testing (k6)
✓ Chaos testing (Simyorg/ChaosMonkey)
```

---

## 15. Eksik / Geliştirilebilir Alanlar

Bu bölüm diğer AI sistemlerin analiz etmesi için açıkça listelenmiştir.

### 15.1 Lisans/Donanım Gerektiren Entegrasyonlar (Üretimde Gerçek — Bu Projede Simüle)

Aşağıdaki bileşenler gerçek bir bankada zorunludur; ancak finansal lisans, fiziksel donanım veya scheme sertifikasyonu gerektirir. Bu projede her biri **gerçek implementasyonla birebir aynı interface'e sahip stub/simulator** olarak yazılacaktır — böylece akış uçtan uca test edilir ve ileride gerçek sağlayıcıya geçiş tek bir adapter değişikliğiyle yapılır. Üretim çözümü referans olarak verilmiştir.

1.  **Gerçek HSM ve Kart Şeması Entegrasyonu:**
    *   *Üretim:* İkişer adet yedekli fiziksel **Thales payShield 10K / Luna** HSM (FIPS 140-2 Level 3), zeroization altyapısı; Visa/Mastercard/TROY için BKM Net fiber hatları ve Switch entegrasyonu.
    *   *Bu projede:* `IHsmClient` ve `ISchemeGateway` arayüzleri arkasında yazılımsal **simulator** (PIN block üretimi/doğrulaması, MAC kontrolü ve onay/ret üreten sahte switch). Gerçek bir anahtara dokunmadan authorization akışı tam olarak test edilir.
2.  **Çekirdek Bankacılık (Core Banking) Paketi:**
    *   *Üretim:* Hazır Core Banking paketi (örn. **Architecht BOA** veya **Intertech inter-Next**) ile yasal defter (General Ledger/THS) ve CIF süreçleri.
    *   *Bu projede:* Core Banking rolünü kendi **Transaction/Ledger servisimiz** üstlenir (double-entry, append-only). `ICoreBankingAdapter` arayüzü ile ileride gerçek pakete geçiş noktası bırakılır.
3.  **Uzaktan Kimlik Tespiti ve KYC:**
    *   *Üretim:* **Techsign / ArkSigner** SDK ile NFC çip okuma, liveness detection, görüntülü görüşme (E2EE, ≥10 yıl saklama) ve Challenge-Response e-imza.
    *   *Bu projede:* `IKycProvider` arayüzü arkasında onaylı/ret senaryolarını döndüren mock; sonuç, Account servisindeki `kyc_status` alanını besler.
4.  **MASAK ve AML Uyum Katmanı:**
    *   *Üretim:* **Fineksus / Sanction Scanner** Inspector API ile gerçek zamanlı PEP/yaptırım taraması.
    *   *Bu projede:* `ISanctionScreeningClient` arayüzü arkasında yerel kara liste tablosuyla çalışan mock; gerçek API'ye geçiş tek satır konfigürasyon değişikliğidir.

### 15.2 Teknik ve Yasal Geliştirme Alanları (Uyum ve Hardening)

1.  **COBIT Uyumlu Dağıtım (CI/CD):**
    *   Geliştirici ve yayına alıcı rollerinin ayrılması (Segregation of Duties) ve kod değişikliklerinin uçtan uca izlenebilirliği.
2.  **5651 Sayılı Kanun Uyumlu Loglama:**
    *   Veri merkezi altyapısındaki tüm erişim ve sistem loglarının TÜBİTAK Zaman Damgası (RFC 3161) ile imzalanarak değiştirilemez şekilde saklanması.
3.  **GDPR/KVKK Veri İmha Politikası ve Mahremiyet:**
    *   Sağlık ve cinsel hayata ilişkin hassas verilerin paylaşılmaması, veri tabanı maskeleme (log scrubber) ve yasal 10 yıllık saklama süresi sonunda otomatik imha/anonimleştirme süreçleri.
4.  **Disaster Recovery (DR) RTO/RPO Limitleri:**
    *   Metro-içi senkron replika ile **RPO = 0**; İstanbul–Ankara bölgeler arası asenkron replikasyon ile **RPO ~dakikalar** ve **RTO ≈ 1 saat** (otomatik failover). Yurt dışı hatlar kopsa dahi yurt içi altyapı çalışmaya devam eder.
5.  **k6 ve Chaos Testleri:**
    *   Canlı öncesi k6 ile BKM ve FAST yük testi simülasyonları (Hedef: 10.000 TPS) ve Simyorg ile chaos senaryoları denemesi.

### 15.3 Mimari Tartışmaya Açık Noktalar
```
1. Event Sourcing Kapsamı
   - Sadece Transaction için mi, tüm servisler için mi?
   - Trade-off: Kompleksite vs. tam audit trail

2. Kafka vs. RabbitMQ
   - Kafka: Yüksek hacim, replay, log-based
   - RabbitMQ: Daha basit, öncelikli queue
   - Öneri: Kafka (finansal event replay kritik)

3. Redis Cluster vs. Redis Sentinel
   - Sentinel: Basit HA
   - Cluster: Horizontal scale
   - Öneri: Limit cache için Cluster (yüksek write TPS)

4. PostgreSQL vs. CockroachDB
   - PostgreSQL: Olgun, iyi anlaşılmış
   - CockroachDB: Distributed, global
   - Öneri: PostgreSQL + Patroni (Türkiye single-region)
```

---

## Proje Yapısı (Solution Layout)

```
MiniBank.sln
│
├── src/
│   ├── Shared/
│   │   ├── MiniBank.Shared.Domain/          # Value objects, base classes
│   │   ├── MiniBank.Shared.Infrastructure/  # Kafka, Redis, EF base
│   │   └── MiniBank.Shared.Contracts/       # Event şemaları, DTO'lar
│   │
│   ├── Services/
│   │   ├── MiniBank.IamService/
│   │   ├── MiniBank.AccountService/
│   │   ├── MiniBank.CardService/
│   │   ├── MiniBank.AuthorizationService/
│   │   ├── MiniBank.TransactionService/
│   │   ├── MiniBank.InstallmentService/
│   │   ├── MiniBank.FraudService/
│   │   ├── MiniBank.EodService/
│   │   ├── MiniBank.ReconciliationService/
│   │   └── MiniBank.NotificationService/
│   │
│   └── ApiGateway/
│       └── MiniBank.ApiGateway/             # YARP
│
├── tests/
│   ├── Unit/
│   │   ├── MiniBank.CardService.UnitTests/
│   │   ├── MiniBank.AuthorizationService.UnitTests/
│   │   └── MiniBank.InstallmentService.UnitTests/
│   │
│   └── Integration/
│       ├── MiniBank.AuthorizationService.IntTests/  # Testcontainers
│       └── MiniBank.EodService.IntTests/
│
├── infra/
│   ├── docker-compose.yml
│   ├── docker-compose.override.yml
│   ├── k8s/
│   │   ├── base/
│   │   └── overlays/
│   │       ├── dev/
│   │       └── prod/
│   └── init-scripts/
│       └── 01-create-databases.sql
│
└── docs/
    ├── ARCHITECTURE.md               # Bu dosya
    ├── adr/                          # Architecture Decision Records
    │   ├── 001-event-sourcing.md
    │   ├── 002-kafka-vs-rabbitmq.md
    │   └── 003-cqrs-approach.md
    └── api/
        └── openapi.yaml
```

---

*Bu doküman MiniBank projesinin canlı mimari referansıdır. Değişiklikler ADR (Architecture Decision Record) süreci ile yönetilir.*
