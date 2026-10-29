# FAZ 1 — SHARED KERNEL & DOMAIN TEMELLERİ (Devam: Adım 1.5 – 1.26)

> **ÖNCEKİ DOSYA:** `IMPLEMENTATION_PLAN.md` — Faz 0 + Faz 1 adım 1.0–1.4
> **ÖN KOŞUL:** Faz 0 tamamen tamamlanmış ve Adım 1.0–1.4 tamamlanmış olmalı.

---

## ADIM 1.5 — Strongly Typed ID'ler

**NEDEN ÖNEMLİ:** Bankacılıkta `Guid accountId` ve `Guid cardId` ikisi de `Guid`'dir. Yanlışlıkla bir account ID'si card ID'si olarak gönderebilirsiniz ve derleyici uyarmaz. Strongly typed ID'ler derleme zamanında bu tür hataları yakalar.

**KURAL:** Her entity'nin kendi ID tipi olacak. Hepsi aynı base record'dan türeyecek.

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Primitives\StronglyTypedId.cs`

```csharp
namespace MiniBank.Shared.Domain.Primitives;

/// <summary>
/// Tüm strongly typed ID'lerin base'i.
/// Guid'i sarar ve tip güvenliği sağlar.
/// Yani AccountId ile CardId karıştırılamaz — derleyici hata verir.
/// </summary>
public abstract record StronglyTypedId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Primitives\EntityIds.cs`

Bu dosyada TÜM entity ID'leri tanımlanacak. Her biri `StronglyTypedId`'den türer.

```csharp
namespace MiniBank.Shared.Domain.Primitives;

public sealed record AccountId(Guid Value) : StronglyTypedId(Value)
{
    public static AccountId New() => new(Guid.NewGuid());
    public static AccountId From(Guid id) => new(id);
}

public sealed record CardId(Guid Value) : StronglyTypedId(Value)
{
    public static CardId New() => new(Guid.NewGuid());
    public static CardId From(Guid id) => new(id);
}

public sealed record CustomerId(Guid Value) : StronglyTypedId(Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public static CustomerId From(Guid id) => new(id);
}

public sealed record TransactionId(Guid Value) : StronglyTypedId(Value)
{
    public static TransactionId New() => new(Guid.NewGuid());
    public static TransactionId From(Guid id) => new(id);
}

public sealed record AuthorizationId(Guid Value) : StronglyTypedId(Value)
{
    public static AuthorizationId New() => new(Guid.NewGuid());
    public static AuthorizationId From(Guid id) => new(id);
}

public sealed record InstallmentPlanId(Guid Value) : StronglyTypedId(Value)
{
    public static InstallmentPlanId New() => new(Guid.NewGuid());
    public static InstallmentPlanId From(Guid id) => new(id);
}

public sealed record UserId(Guid Value) : StronglyTypedId(Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid id) => new(id);
}

public sealed record LedgerEntryId(Guid Value) : StronglyTypedId(Value)
{
    public static LedgerEntryId New() => new(Guid.NewGuid());
    public static LedgerEntryId From(Guid id) => new(id);
}
```

**TEST OLUŞTUR:** `tests\Unit\MiniBank.Shared.Domain.UnitTests\Primitives\StronglyTypedIdTests.cs`

```csharp
using FluentAssertions;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.Shared.Domain.UnitTests.Primitives;

public class StronglyTypedIdTests
{
    [Fact]
    public void New_ShouldCreateUniqueIds()
    {
        var id1 = AccountId.New();
        var id2 = AccountId.New();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void From_ShouldWrapExistingGuid()
    {
        var guid = Guid.NewGuid();
        var id = AccountId.From(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void SameGuid_DifferentTypes_ShouldNotBeEqual()
    {
        var guid = Guid.NewGuid();
        var accountId = AccountId.From(guid);
        var cardId = CardId.From(guid);

        // Bu derleme zamanında zaten karşılaştırılamaz,
        // ama runtime'da da farklı olduklarını doğrulayalım.
        accountId.Value.Should().Be(cardId.Value); // aynı Guid
        accountId.GetType().Should().NotBe(cardId.GetType()); // farklı tip
    }

    [Fact]
    public void SameGuid_SameType_ShouldBeEqual()
    {
        var guid = Guid.NewGuid();
        var id1 = CardId.From(guid);
        var id2 = CardId.From(guid);

        id1.Should().Be(id2);
    }

    [Fact]
    public void ToString_ShouldReturnGuidString()
    {
        var guid = Guid.NewGuid();
        var id = TransactionId.From(guid);

        id.ToString().Should().Be(guid.ToString());
    }
}
```

**DOĞRULAMA:**
```powershell
dotnet test tests\Unit\MiniBank.Shared.Domain.UnitTests
# Beklenen: tüm testler geçer
```

- [ ] Tamamlandı

---

## ADIM 1.6 — Pan Value Object (Kart Numarası)

**NEDEN ÖNEMLİ:** PAN (Primary Account Number) kart numarasıdır (16 haneli). Bankacılıkta PAN asla açık olarak:
- Loglanmaz (PCI-DSS ihlali)
- Veritabanına yazılmaz (hash'lenir veya tokenize edilir)
- API'de döndürülmez (maskelenir: 4532****1234)

**KURALLAR:**
- Luhn algoritması ile doğrulama (kart numarasının geçerli olup olmadığını kontrol eden mod 10 algoritması)
- `Masked` property → `"453212******1234"` formatında döner
- `Hashed` property → SHA-256 hash döner (DB'de saklanacak)
- `ToString()` → DAİMA maskelenmiş halini döner (kazara loglanmayı önler)
- `Last4` → son 4 hane (dekont, SMS bildirimi için)

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\ValueObjects\Pan.cs`

```csharp
using System.Security.Cryptography;
using System.Text;

namespace MiniBank.Shared.Domain.ValueObjects;

/// <summary>
/// Primary Account Number — kart numarası (16 hane).
/// PAN asla açık metin olarak loglanmaz veya saklanmaz.
/// ToString() DAİMA maskelenmiş halini döner.
/// </summary>
public sealed class Pan : IEquatable<Pan>
{
    private readonly string _value;

    private Pan(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Yeni Pan oluşturur. Luhn doğrulaması yapar.
    /// </summary>
    /// <param name="panNumber">16 haneli kart numarası (boşluksuz, sadece rakam)</param>
    public static Pan Create(string panNumber)
    {
        if (string.IsNullOrWhiteSpace(panNumber))
            throw new ArgumentException("PAN cannot be empty.", nameof(panNumber));

        var cleaned = panNumber.Replace(" ", "").Replace("-", "");

        if (cleaned.Length < 13 || cleaned.Length > 19)
            throw new ArgumentException("PAN must be 13-19 digits.", nameof(panNumber));

        if (!cleaned.All(char.IsDigit))
            throw new ArgumentException("PAN must contain only digits.", nameof(panNumber));

        if (!PassesLuhn(cleaned))
            throw new ArgumentException("PAN failed Luhn check.", nameof(panNumber));

        return new Pan(cleaned);
    }

    /// <summary>
    /// Son 4 hane. Dekont ve SMS'lerde kullanılır.
    /// Örnek: "1234"
    /// </summary>
    public string Last4 => _value[^4..];

    /// <summary>
    /// İlk 6 hane (BIN). Kartın bankasını ve tipini belirler.
    /// </summary>
    public string Bin => _value[..6];

    /// <summary>
    /// Maskelenmiş PAN. API response'larda kullanılır.
    /// Örnek: "453212******1234"
    /// </summary>
    public string Masked => $"{_value[..6]}{"".PadLeft(_value.Length - 10, '*')}{Last4}";

    /// <summary>
    /// SHA-256 hash. Veritabanında PAN yerine bu saklanır.
    /// </summary>
    public string Hashed
    {
        get
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_value));
            return Convert.ToHexStringLower(bytes);
        }
    }

    /// <summary>
    /// Luhn algoritması (Mod 10).
    /// Kart numarasının geçerliliğini kontrol eder.
    /// Nasıl çalışır:
    /// 1. Sağdan sola, çift indexteki rakamları 2 ile çarp
    /// 2. Çarpım 9'dan büyükse 9 çıkar
    /// 3. Tüm rakamları topla
    /// 4. Toplam 10'a tam bölünüyorsa geçerli
    /// </summary>
    private static bool PassesLuhn(string number)
    {
        var sum = 0;
        var alternate = false;

        for (var i = number.Length - 1; i >= 0; i--)
        {
            var digit = number[i] - '0';

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// DAİMA maskelenmiş PAN döner.
    /// Bu sayede Console.WriteLine(pan) veya log.Info(pan) yapıldığında
    /// açık kart numarası ASLA loglanmaz.
    /// </summary>
    public override string ToString() => Masked;

    public bool Equals(Pan? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => obj is Pan other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
}
```

**TEST OLUŞTUR:** `tests\Unit\MiniBank.Shared.Domain.UnitTests\ValueObjects\PanTests.cs`

```csharp
using FluentAssertions;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.UnitTests.ValueObjects;

public class PanTests
{
    // 4532015112830366 → geçerli bir test PAN (Luhn geçer)
    private const string ValidPan = "4532015112830366";

    [Fact]
    public void Create_ValidPan_ShouldSucceed()
    {
        var pan = Pan.Create(ValidPan);

        pan.Should().NotBeNull();
    }

    [Fact]
    public void Create_InvalidLuhn_ShouldThrow()
    {
        var act = () => Pan.Create("4532015112830367"); // son hane yanlış

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Luhn*");
    }

    [Fact]
    public void Create_TooShort_ShouldThrow()
    {
        var act = () => Pan.Create("12345");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithSpaces_ShouldCleanAndAccept()
    {
        var pan = Pan.Create("4532 0151 1283 0366");

        pan.Last4.Should().Be("0366");
    }

    [Fact]
    public void Last4_ShouldReturnLastFourDigits()
    {
        var pan = Pan.Create(ValidPan);

        pan.Last4.Should().Be("0366");
    }

    [Fact]
    public void Bin_ShouldReturnFirstSixDigits()
    {
        var pan = Pan.Create(ValidPan);

        pan.Bin.Should().Be("453201");
    }

    [Fact]
    public void Masked_ShouldHideMiddleDigits()
    {
        var pan = Pan.Create(ValidPan);

        pan.Masked.Should().Contain("******");
        pan.Masked.Should().StartWith("453201");
        pan.Masked.Should().EndWith("0366");
    }

    [Fact]
    public void ToString_ShouldReturnMasked_NeverClear()
    {
        var pan = Pan.Create(ValidPan);

        pan.ToString().Should().NotBe(ValidPan);
        pan.ToString().Should().Contain("*");
    }

    [Fact]
    public void Hashed_ShouldBeConsistent()
    {
        var pan1 = Pan.Create(ValidPan);
        var pan2 = Pan.Create(ValidPan);

        pan1.Hashed.Should().Be(pan2.Hashed);
    }

    [Fact]
    public void Hashed_DifferentPans_ShouldBeDifferent()
    {
        var pan1 = Pan.Create("4532015112830366");
        var pan2 = Pan.Create("5425233430109903"); // farklı geçerli PAN

        pan1.Hashed.Should().NotBe(pan2.Hashed);
    }

    [Fact]
    public void Equality_SamePan_ShouldBeEqual()
    {
        var pan1 = Pan.Create(ValidPan);
        var pan2 = Pan.Create(ValidPan);

        pan1.Should().Be(pan2);
    }
}
```

**DOĞRULAMA:**
```powershell
dotnet test tests\Unit\MiniBank.Shared.Domain.UnitTests --filter "PanTests"
# Beklenen: tüm testler geçer
```

- [ ] Tamamlandı

---

## ADIM 1.7 — Iban Value Object

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\ValueObjects\Iban.cs`

```csharp
using System.Numerics;
using System.Text.RegularExpressions;

namespace MiniBank.Shared.Domain.ValueObjects;

/// <summary>
/// International Bank Account Number.
/// Türkiye formatı: TR + 2 kontrol hane + 5 banka kodu + 16 hesap no = toplam 26 karakter.
/// Doğrulama: MOD 97 algoritması (ISO 13616).
/// </summary>
public sealed partial class Iban : IEquatable<Iban>
{
    private readonly string _value;

    private Iban(string value)
    {
        _value = value;
    }

    /// <summary>
    /// IBAN oluşturur ve MOD 97 doğrulaması yapar.
    /// </summary>
    public static Iban Create(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            throw new ArgumentException("IBAN cannot be empty.", nameof(iban));

        var cleaned = iban.Replace(" ", "").ToUpperInvariant();

        if (!IbanPattern().IsMatch(cleaned))
            throw new ArgumentException($"Invalid IBAN format: '{iban}'.", nameof(iban));

        if (!PassesMod97(cleaned))
            throw new ArgumentException($"IBAN failed MOD 97 check: '{iban}'.", nameof(iban));

        return new Iban(cleaned);
    }

    public string Value => _value;

    /// <summary>
    /// Görüntüleme formatı: TR12 3456 7890 1234 5678 9012 34
    /// </summary>
    public string Formatted
    {
        get
        {
            var parts = new List<string>();
            for (var i = 0; i < _value.Length; i += 4)
                parts.Add(_value.Substring(i, Math.Min(4, _value.Length - i)));
            return string.Join(" ", parts);
        }
    }

    /// <summary>
    /// Banka kodu (5 hane, pozisyon 4-8).
    /// </summary>
    public string BankCode => _value.Substring(4, 5);

    /// <summary>
    /// MOD 97 algoritması (ISO 13616):
    /// 1. İlk 4 karakteri (ülke kodu + kontrol hane) sona taşı
    /// 2. Harfleri sayıya çevir (A=10, B=11, ... Z=35)
    /// 3. Elde edilen büyük sayıyı 97'ye böl
    /// 4. Kalan 1 ise geçerli
    /// </summary>
    private static bool PassesMod97(string iban)
    {
        var rearranged = iban[4..] + iban[..4];

        var numericString = new System.Text.StringBuilder();
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c))
                numericString.Append(c);
            else
                numericString.Append(c - 'A' + 10);
        }

        var number = BigInteger.Parse(numericString.ToString());
        return number % 97 == 1;
    }

    public override string ToString() => Formatted;

    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{4,30}$")]
    private static partial Regex IbanPattern();

    public bool Equals(Iban? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => obj is Iban other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
}
```

**TEST OLUŞTUR:** `tests\Unit\MiniBank.Shared.Domain.UnitTests\ValueObjects\IbanTests.cs`

```csharp
using FluentAssertions;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.UnitTests.ValueObjects;

public class IbanTests
{
    // Geçerli test IBAN'ı (MOD 97 geçer)
    private const string ValidTrIban = "TR330006100519786457841326";

    [Fact]
    public void Create_ValidIban_ShouldSucceed()
    {
        var iban = Iban.Create(ValidTrIban);
        iban.Value.Should().Be(ValidTrIban);
    }

    [Fact]
    public void Create_WithSpaces_ShouldClean()
    {
        var iban = Iban.Create("TR33 0006 1005 1978 6457 8413 26");
        iban.Value.Should().Be(ValidTrIban);
    }

    [Fact]
    public void Create_Lowercase_ShouldUppercase()
    {
        var iban = Iban.Create("tr330006100519786457841326");
        iban.Value.Should().Be(ValidTrIban);
    }

    [Fact]
    public void Create_InvalidMod97_ShouldThrow()
    {
        var act = () => Iban.Create("TR330006100519786457841327"); // son hane değişti
        act.Should().Throw<ArgumentException>().WithMessage("*MOD 97*");
    }

    [Fact]
    public void BankCode_ShouldReturnCorrectCode()
    {
        var iban = Iban.Create(ValidTrIban);
        iban.BankCode.Should().Be("00061");
    }

    [Fact]
    public void Formatted_ShouldHaveSpaces()
    {
        var iban = Iban.Create(ValidTrIban);
        iban.Formatted.Should().Contain(" ");
    }
}
```

- [ ] Tamamlandı

---

## ADIM 1.8 — NationalId Value Object (TC Kimlik No)

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\ValueObjects\NationalId.cs`

```csharp
namespace MiniBank.Shared.Domain.ValueObjects;

/// <summary>
/// TC Kimlik Numarası. 11 haneli, algoritmik doğrulama ile kontrol edilir.
/// KVKK kapsamında kişisel veridir — loglarda maskelenmeli.
/// </summary>
public sealed class NationalId : IEquatable<NationalId>
{
    private readonly string _value;

    private NationalId(string value) => _value = value;

    /// <summary>
    /// TC Kimlik No doğrulaması:
    /// 1. 11 haneli olmalı
    /// 2. İlk hane 0 olamaz
    /// 3. İlk 10 hane → 11. hane kontrol rakamı
    /// 4. Tek pozisyon toplamı * 7 - çift pozisyon toplamı MOD 10 = 10. hane
    /// 5. İlk 10 hanenin toplamı MOD 10 = 11. hane
    /// </summary>
    public static NationalId Create(string nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
            throw new ArgumentException("National ID cannot be empty.", nameof(nationalId));

        var cleaned = nationalId.Trim();

        if (cleaned.Length != 11 || !cleaned.All(char.IsDigit))
            throw new ArgumentException("National ID must be 11 digits.", nameof(nationalId));

        if (cleaned[0] == '0')
            throw new ArgumentException("National ID cannot start with 0.", nameof(nationalId));

        var digits = cleaned.Select(c => c - '0').ToArray();

        // 10. hane kontrolü
        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        var tenthDigit = (oddSum * 7 - evenSum) % 10;
        if (tenthDigit < 0) tenthDigit += 10;

        if (digits[9] != tenthDigit)
            throw new ArgumentException("National ID checksum invalid (digit 10).", nameof(nationalId));

        // 11. hane kontrolü
        var firstTenSum = digits.Take(10).Sum();
        if (digits[10] != firstTenSum % 10)
            throw new ArgumentException("National ID checksum invalid (digit 11).", nameof(nationalId));

        return new NationalId(cleaned);
    }

    public string Value => _value;

    /// <summary>
    /// Maskelenmiş hali. KVKK uyumlu — sadece son 4 hane görünür.
    /// Örnek: "*******1234"
    /// </summary>
    public string Masked => $"*******{_value[^4..]}";

    public override string ToString() => Masked;

    public bool Equals(NationalId? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => obj is NationalId other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
}
```

**TEST OLUŞTUR:** `tests\Unit\MiniBank.Shared.Domain.UnitTests\ValueObjects\NationalIdTests.cs`

```csharp
using FluentAssertions;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.UnitTests.ValueObjects;

public class NationalIdTests
{
    // Algoritmik olarak geçerli test TC no
    private const string ValidId = "10000000146";

    [Fact]
    public void Create_ValidId_ShouldSucceed()
    {
        var id = NationalId.Create(ValidId);
        id.Value.Should().Be(ValidId);
    }

    [Fact]
    public void Create_InvalidChecksum_ShouldThrow()
    {
        var act = () => NationalId.Create("10000000147"); // yanlış son hane
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_StartsWithZero_ShouldThrow()
    {
        var act = () => NationalId.Create("01234567890");
        act.Should().Throw<ArgumentException>().WithMessage("*start with 0*");
    }

    [Fact]
    public void Create_WrongLength_ShouldThrow()
    {
        var act = () => NationalId.Create("123456");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Masked_ShouldHideFirstDigits()
    {
        var id = NationalId.Create(ValidId);
        id.Masked.Should().StartWith("*******");
        id.Masked.Should().EndWith("0146");
    }

    [Fact]
    public void ToString_ShouldReturnMasked()
    {
        var id = NationalId.Create(ValidId);
        id.ToString().Should().NotBe(ValidId);
        id.ToString().Should().Contain("*");
    }
}
```

- [ ] Tamamlandı

---

## ADIM 1.9 — CardExpiry Value Object

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\ValueObjects\CardExpiry.cs`

```csharp
namespace MiniBank.Shared.Domain.ValueObjects;

/// <summary>
/// Kart son kullanma tarihi (MM/YY).
/// POS terminallerden YYMM veya MMYY olarak gelebilir.
/// Bu value object her durumu handle eder ve tutarlı bir format sunar.
/// </summary>
public sealed record CardExpiry
{
    public int Month { get; }
    public int Year { get; }

    private CardExpiry(int month, int year)
    {
        Month = month;
        Year = year;
    }

    /// <summary>
    /// MM/YY formatından oluşturur. Örnek: "12/25" → Aralık 2025
    /// </summary>
    public static CardExpiry Create(int month, int year)
    {
        if (month < 1 || month > 12)
            throw new ArgumentException("Month must be 1-12.", nameof(month));

        // 2 haneli yıl → 4 haneli yıla çevir
        var fullYear = year < 100 ? 2000 + year : year;

        if (fullYear < 2020 || fullYear > 2099)
            throw new ArgumentException("Year must be between 2020 and 2099.", nameof(year));

        return new CardExpiry(month, fullYear);
    }

    /// <summary>
    /// String'den parse eder. "12/25", "1225", "2512" formatlarını kabul eder.
    /// </summary>
    public static CardExpiry Parse(string expiry)
    {
        if (string.IsNullOrWhiteSpace(expiry))
            throw new ArgumentException("Expiry cannot be empty.", nameof(expiry));

        var cleaned = expiry.Replace("/", "").Replace("-", "");

        if (cleaned.Length != 4 || !cleaned.All(char.IsDigit))
            throw new ArgumentException("Expiry must be 4 digits (MMYY).", nameof(expiry));

        var month = int.Parse(cleaned[..2]);
        var year = int.Parse(cleaned[2..]);

        return Create(month, year);
    }

    /// <summary>
    /// Kartın bugün itibariyle geçerliliğini kontrol eder.
    /// Kartlar son kullanma ayının son günü gece yarısına kadar geçerlidir.
    /// </summary>
    public bool IsExpired(DateTime utcNow)
    {
        var expiryDate = new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month), 23, 59, 59);
        return utcNow > expiryDate;
    }

    /// <summary>
    /// MM/YY formatında döner.
    /// </summary>
    public override string ToString() => $"{Month:D2}/{Year % 100:D2}";
}
```

- [ ] Tamamlandı

---

## ADIM 1.10 — Domain Enum'ları

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\AccountType.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

public enum AccountType
{
    Checking = 1,    // Vadesiz mevduat
    Savings = 2,     // Vadeli mevduat
    Current = 3,     // Cari hesap (ticari)
    Credit = 4       // Kredi hesabı
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\AccountStatus.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

public enum AccountStatus
{
    Active = 1,
    Frozen = 2,      // Mahkeme kararı veya MASAK
    Closed = 3,
    Dormant = 4      // 1 yıl hareketsiz
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\CardType.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

public enum CardType
{
    Debit = 1,       // Banka kartı — hesaptaki paradan çekilir
    Credit = 2,      // Kredi kartı — limit üzerinden çalışır
    Prepaid = 3      // Ön ödemeli kart
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\CardStatus.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

/// <summary>
/// Kart durumu state machine.
/// Geçerli geçişler:
///   Created → Active (aktivasyon sonrası)
///   Active → Blocked (kullanıcı talebi veya fraud)
///   Active → StolenLost (kayıp/çalıntı bildirimi)
///   Blocked → Active (bloke kaldırma)
///   Active → Expired (son kullanma tarihi geçti)
///   Active → Closed (iptal)
///   Expired → Closed (kesin kapatma)
///   StolenLost → Closed (kesin kapatma)
/// </summary>
public enum CardStatus
{
    Created = 1,
    Active = 2,
    Blocked = 3,
    StolenLost = 4,
    Expired = 5,
    Closed = 6
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\TransactionType.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

public enum TransactionType
{
    Purchase = 1,       // Satın alma
    Refund = 2,         // İade
    CashAdvance = 3,    // Nakit avans
    Payment = 4,        // Borç ödeme
    Transfer = 5,       // Havale/EFT
    Fee = 6,            // Ücret/komisyon
    Interest = 7,       // Faiz
    Reversal = 8        // İptal (void)
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\TransactionStatus.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

public enum TransactionStatus
{
    Pending = 1,        // Provizyon alındı, henüz kesinleşmedi
    Approved = 2,       // Onaylandı
    Declined = 3,       // Reddedildi
    Settled = 4,        // Takas sonucu kesinleşti (EOD)
    Reversed = 5,       // İptal edildi (void)
    Refunded = 6        // İade edildi
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\EntryType.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

/// <summary>
/// Çift taraflı muhasebede kayıt yönü.
/// HER işlemde bir DEBIT ve bir CREDIT kaydı ZORUNLUDUR.
/// Toplam DEBIT = Toplam CREDIT olmalı (DR = CR invariant).
/// </summary>
public enum EntryType
{
    Debit = 1,   // Borç kaydı (DR) — varlık artışı veya borç azalışı
    Credit = 2   // Alacak kaydı (CR) — varlık azalışı veya borç artışı
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\LimitType.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

/// <summary>
/// Limit türleri. Her limit farklı bir zaman periyodunda çalışır.
/// </summary>
public enum LimitType
{
    SingleTransaction = 1,  // Tek işlem limiti
    Daily = 2,              // Günlük limit (00:00-23:59 UTC)
    Weekly = 3,             // Haftalık limit
    Monthly = 4             // Aylık limit (ayın 1'i - son günü)
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Enums\DeclineReason.cs`

```csharp
namespace MiniBank.Shared.Domain.Enums;

/// <summary>
/// İşlem red nedenleri. ISO 8583 Field 39 (Response Code) karşılıkları.
/// </summary>
public enum DeclineReason
{
    None = 0,
    InsufficientFunds = 1,    // Yetersiz bakiye/limit → RC 51
    CardNotActive = 2,        // Kart aktif değil → RC 14
    CardExpired = 3,          // Kart süresi dolmuş → RC 54
    LimitExceeded = 4,        // Limit aşıldı → RC 61
    FraudSuspected = 5,       // Dolandırıcılık şüphesi → RC 59
    InvalidMerchant = 6,      // Geçersiz üye iş yeri → RC 03
    InvalidPin = 7,           // Yanlış PIN → RC 55
    SystemError = 8           // Sistem hatası → RC 96
}
```

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
# Beklenen: Build succeeded, 0 error, 0 warning
```

- [ ] Tamamlandı

---

## ADIM 1.11 — Entity Base Class ve AggregateRoot

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Primitives\Entity.cs`

```csharp
namespace MiniBank.Shared.Domain.Primitives;

/// <summary>
/// Tüm domain entity'lerinin base class'ı.
/// Her entity'nin:
/// - Bir ID'si var (generic TId — strongly typed)
/// - CreatedAt / UpdatedAt audit timestamp'leri var
/// - Version'ı var (optimistic concurrency için)
/// </summary>
public abstract class Entity<TId> where TId : StronglyTypedId
{
    public TId Id { get; protected set; } = default!;
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }
    public int Version { get; protected set; }

    protected Entity() { }

    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = 1;
    }

    /// <summary>
    /// Entity değiştirildiğinde çağrılır.
    /// UpdatedAt timestamp'ini günceller.
    /// </summary>
    protected void MarkModified()
    {
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Primitives\AggregateRoot.cs`

```csharp
namespace MiniBank.Shared.Domain.Primitives;

/// <summary>
/// DDD Aggregate Root. Entity'den türer, ek olarak domain event'leri yönetir.
/// 
/// Aggregate Root, bir consistency boundary'dir:
/// - Dışarıdan sadece root üzerinden erişilir
/// - İç entity'lere doğrudan erişilmez
/// - Bir transaction'da sadece bir aggregate değiştirilir
/// 
/// Domain event'ler: Aggregate içinde biriken event'ler, SaveChanges sonrasında
/// Outbox'a yazılır ve Kafka'ya gönderilir.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : StronglyTypedId
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot() { }
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Biriken domain event'leri okur (read-only).
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Yeni bir domain event ekler. SaveChanges'dan önce çağrılır.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Event'leri temizler. Outbox'a yazıldıktan sonra çağrılır.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Primitives\IDomainEvent.cs`

```csharp
namespace MiniBank.Shared.Domain.Primitives;

/// <summary>
/// Domain event marker interface.
/// Aggregate içinde oluşan önemli olayları temsil eder.
/// Outbox pattern ile Kafka'ya gönderilir.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
```

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
# Beklenen: Build succeeded, 0 error
```

- [ ] Tamamlandı

---

## ADIM 1.12 — Integration Event Base ve Outbox Entity

**NE YAPILACAK:** Servisler arası iletişim için integration event'ler ve Outbox pattern entity'si.

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Contracts\Events\IntegrationEvent.cs`

```csharp
namespace MiniBank.Shared.Contracts.Events;

/// <summary>
/// Servisler arası event'lerin base record'u.
/// Kafka'ya JSON olarak gönderilir.
/// Her event'te:
/// - EventId: Benzersiz kimlik (idempotency için)
/// - OccurredAt: Oluşma zamanı
/// - CorrelationId: İstekleri birbirine bağlayan ID (tracing)
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Persistence\OutboxMessage.cs`

```csharp
namespace MiniBank.Shared.Infrastructure.Persistence;

/// <summary>
/// Outbox pattern entity.
/// Domain event → JSON → OutboxMessage → aynı DB transaction'da yazılır.
/// Sonra bir BackgroundService bu tablodan okur ve Kafka'ya gönderir.
/// 
/// NEDEN: Kafka'ya doğrudan event göndersek, DB commit ama Kafka fail ederse
/// veri tutarsızlığı olur. Outbox ile ikisi aynı transaction'dadır.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
```

- [ ] Tamamlandı

---

## ADIM 1.13 — Result<T> Pattern

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Domain\Primitives\Result.cs`

```csharp
namespace MiniBank.Shared.Domain.Primitives;

/// <summary>
/// Railway-oriented programming pattern.
/// Exception fırlatmak yerine Result döner.
/// API controller'da: result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error)
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        Error = null;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _value = default;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string entity, string id)
        => new($"{entity.ToUpperInvariant()}_NOT_FOUND", $"{entity} with ID '{id}' was not found.");

    public static Error Validation(string code, string message)
        => new(code, message);

    public static Error Conflict(string code, string message)
        => new(code, message);
}
```

- [ ] Tamamlandı

---

## ADIM 1.14 — Clock Abstraction

**NEDEN ÖNEMLİ:** Testlerde `DateTime.UtcNow` kullanırsanız zamana bağlı testler yazmak imkansızdır. `IClock` ile zamanı kontrol edebilirsiniz.

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Clock\IClock.cs`

```csharp
namespace MiniBank.Shared.Infrastructure.Clock;

public interface IClock
{
    DateTime UtcNow { get; }
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Clock\SystemClock.cs`

```csharp
namespace MiniBank.Shared.Infrastructure.Clock;

/// <summary>
/// Gerçek sistem saati. Production'da DI'a bu register edilir.
/// services.AddSingleton<IClock, SystemClock>();
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Clock\FakeClock.cs`

```csharp
namespace MiniBank.Shared.Infrastructure.Clock;

/// <summary>
/// Test'lerde zamanı kontrol etmek için kullanılır.
/// Örnek: kart son kullanma tarihi testi → 2026'ya ilerlet, expired mı kontrol et.
/// </summary>
public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    public void SetTo(DateTime dateTime) => UtcNow = dateTime;
}
```

- [ ] Tamamlandı

---

## ADIM 1.15 — Infrastructure NuGet Paketleri Ekleme

**NE YAPILACAK:** Shared.Infrastructure projesine gerekli NuGet paketlerini ekle.

**KOMUTLAR:**

```powershell
# EF Core
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package Microsoft.EntityFrameworkCore
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL

# Redis
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package StackExchange.Redis

# Kafka / MassTransit
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package MassTransit
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package MassTransit.Kafka

# Serilog
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package Serilog.AspNetCore

# ASP.NET Core (middleware'ler için)
dotnet add src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
```

**ÖNEMLİ:** `.csproj` dosyalarına `Version` eklenmemeli. Central Package Management dosyasından (`Directory.Packages.props`) otomatik çekilir.

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
# Beklenen: Build succeeded
```

- [ ] Tamamlandı

---

## ADIM 1.16 — BaseDbContext (EF Core)

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Persistence\BaseDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;

namespace MiniBank.Shared.Infrastructure.Persistence;

/// <summary>
/// Tüm servis DbContext'lerinin base class'ı.
/// Ortak davranışlar:
/// 1. Outbox tablosu her DB'de var
/// 2. SaveChanges sırasında audit field'lar otomatik güncellenir
/// 3. Domain event'ler outbox'a otomatik yazılır
/// </summary>
public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options) { }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Outbox tablosu konfigürasyonu
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.OccurredAt).IsRequired();
            entity.HasIndex(e => e.ProcessedAt)
                .HasFilter("processed_at IS NULL")
                .HasDatabaseName("ix_outbox_unprocessed");
        });
    }

    /// <summary>
    /// SaveChanges override — aggregate'lerin domain event'lerini outbox'a yazar.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Domain event'leri outbox'a dönüştür
        var aggregates = ChangeTracker.Entries()
            .Where(e => e.Entity is IAggregateWithEvents)
            .Select(e => (IAggregateWithEvents)e.Entity)
            .ToList();

        var outboxMessages = new List<OutboxMessage>();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.GetDomainEvents())
            {
                outboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().Name,
                    Content = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAt = DateTime.UtcNow
                });
            }

            aggregate.ClearEvents();
        }

        if (outboxMessages.Count > 0)
            await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);

        // Audit field'ları güncelle
        UpdateAuditFields();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;

            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") is not null)
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Domain event'leri olan aggregate'lerin implement ettiği interface.
/// BaseDbContext bu interface'e bakarak event'leri toplar.
/// </summary>
public interface IAggregateWithEvents
{
    IReadOnlyCollection<object> GetDomainEvents();
    void ClearEvents();
}
```

**DOĞRULAMA:**
```powershell
dotnet build MiniBank.sln
# Beklenen: Build succeeded
```

- [ ] Tamamlandı

---

## ADIM 1.17 — Outbox Background Worker

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Messaging\OutboxProcessor.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.Shared.Infrastructure.Messaging;

/// <summary>
/// Background service — her 5 saniyede outbox tablosunu kontrol eder.
/// İşlenmemiş mesajları alır, Kafka'ya gönderir, başarılıysa ProcessedAt'i günceller.
/// 
/// Bu servis HER mikroserviste DI'a register edilir:
/// services.AddHostedService<OutboxProcessor<MyDbContext>>();
/// </summary>
public sealed class OutboxProcessor<TDbContext> : BackgroundService
    where TDbContext : BaseDbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor<TDbContext>> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started for {DbContext}.", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxProcessor error.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.OccurredAt)
            .Take(20) // batch size
            .ToListAsync(cancellationToken);

        if (messages.Count == 0) return;

        _logger.LogInformation("Processing {Count} outbox messages.", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                // TODO: Faz 2'de Kafka publisher buraya eklenecek.
                // Şimdilik sadece "işlendi" olarak işaretle.
                message.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Outbox message {Id} of type {Type} processed.",
                    message.Id, message.Type);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                _logger.LogWarning(
                    "Outbox message {Id} failed (attempt {Attempt}): {Error}",
                    message.Id, message.RetryCount, ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] Tamamlandı

---

## ADIM 1.18 — Redis Cache Service

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Caching\ICacheService.cs`

```csharp
namespace MiniBank.Shared.Infrastructure.Caching;

/// <summary>
/// Redis cache abstraction.
/// Servisler bu interface üzerinden cache'e erişir.
/// Test'lerde mock'lanabilir.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Atomic decrement. Limit kontrolü için kullanılır.
    /// Redis'te Lua script ile atomik olarak:
    ///   1. Kalan limiti oku
    ///   2. amount >= kalan ise reddet
    ///   3. Değilse düş ve yeni değeri dön
    /// Tüm bu işlem tek bir Redis roundtrip'te olur.
    /// </summary>
    Task<long> DecrementIfSufficientAsync(string key, long amount, CancellationToken ct = default);
}
```

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Caching\RedisCacheService.cs`

```csharp
using System.Text.Json;
using StackExchange.Redis;

namespace MiniBank.Shared.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private IDatabase Db => _redis.GetDatabase();

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = await Db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await Db.StringSetAsync(key, json, expiry);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await Db.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await Db.KeyExistsAsync(key);
    }

    /// <summary>
    /// Lua script ile atomik limit kontrolü.
    /// Eğer mevcut değer >= amount ise düşür ve yeni değeri dön.
    /// Aksi halde -1 dön (limit yetersiz).
    /// 
    /// Bu Lua script Redis server'da atomik çalışır — race condition OLMAZ.
    /// 100 concurrent request gelse bile her biri sırayla işlenir.
    /// </summary>
    public async Task<long> DecrementIfSufficientAsync(string key, long amount, CancellationToken ct = default)
    {
        const string luaScript = """
            local current = tonumber(redis.call('GET', KEYS[1]) or '0')
            if current >= tonumber(ARGV[1]) then
                return redis.call('DECRBY', KEYS[1], ARGV[1])
            else
                return -1
            end
            """;

        var result = await Db.ScriptEvaluateAsync(luaScript,
            [(RedisKey)key],
            [(RedisValue)amount]);

        return (long)result;
    }
}
```

- [ ] Tamamlandı

---

## ADIM 1.19 — Idempotency Middleware

**NEDEN ÖNEMLİ:** Ağ kesintisinde istemci aynı isteği tekrar gönderebilir. Idempotency key ile aynı işlem iki kez yapılmaz.

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Idempotency\IdempotencyMiddleware.cs`

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MiniBank.Shared.Infrastructure.Caching;

namespace MiniBank.Shared.Infrastructure.Idempotency;

/// <summary>
/// HTTP isteğinde "Idempotency-Key" header'ı varsa:
/// 1. Bu key daha önce işlendi mi? (Redis'ten kontrol)
/// 2. Evet → cached response'u dön, işlem TEKRAR yapılmaz
/// 3. Hayır → isteği işle, response'u Redis'e kaydet
///
/// Kullanım: POST/PUT isteklerinde client bir UUID gönderir.
/// Aynı UUID ile tekrar istek gelirse, ilk response döner.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICacheService cache)
    {
        // Sadece POST ve PUT isteklerine uygula
        if (context.Request.Method is not ("POST" or "PUT"))
        {
            await _next(context);
            return;
        }

        // Header'dan idempotency key oku
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey) ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";

        // Daha önce işlendi mi?
        var cached = await cache.GetAsync<CachedResponse>(cacheKey);
        if (cached is not null)
        {
            _logger.LogInformation("Idempotent request detected: {Key}. Returning cached response.", idempotencyKey);
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cached.Body);
            return;
        }

        // İsteği işle ama response'u yakala
        var originalBody = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await _next(context);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

        // Response'u cache'e yaz (24 saat TTL)
        var response = new CachedResponse(context.Response.StatusCode, responseBody);
        await cache.SetAsync(cacheKey, response, TimeSpan.FromHours(24));

        // Asıl response'u client'a yaz
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private sealed record CachedResponse(int StatusCode, string Body);
}
```

- [ ] Tamamlandı

---

## ADIM 1.20 — Global Exception Handler Middleware

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Api\GlobalExceptionHandler.cs`

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MiniBank.Shared.Domain.Exceptions;

namespace MiniBank.Shared.Infrastructure.Api;

/// <summary>
/// Tüm exception'ları yakalar ve RFC 7807 Problem Details formatında döner.
/// 
/// DomainException → 422 Unprocessable Entity
/// ConcurrencyConflictException → 409 Conflict
/// ArgumentException → 400 Bad Request
/// Diğer → 500 Internal Server Error
///
/// Bu middleware her servisin pipeline'ına eklenir:
/// app.UseMiddleware<GlobalExceptionHandler>();
/// </summary>
public sealed class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode) = exception switch
        {
            ConcurrencyConflictException => (HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT"),
            DomainException domainEx => (HttpStatusCode.UnprocessableEntity, domainEx.Code),
            ArgumentException => (HttpStatusCode.BadRequest, "INVALID_ARGUMENT"),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
        };

        var problemDetails = new
        {
            type = $"https://minibank.dev/errors/{errorCode.ToLowerInvariant()}",
            title = exception.Message,
            status = (int)statusCode,
            detail = exception is DomainException ? exception.Message : "An unexpected error occurred.",
            errorCode,
            traceId = context.TraceIdentifier
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
```

- [ ] Tamamlandı

---

## ADIM 1.21 — PAN Log Scrubber (Serilog Enricher)

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Logging\PanScrubber.cs`

```csharp
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace MiniBank.Shared.Infrastructure.Logging;

/// <summary>
/// PCI-DSS uyumluluk: Loglarda kart numarası geçerse maskeleme yapar.
/// 13-19 haneli ardışık rakamları (potansiyel PAN) tespit eder ve maskeler.
/// 
/// Serilog pipeline'ına eklenir:
/// .Enrich.With<PanScrubber>()
/// </summary>
public sealed partial class PanScrubber : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // MessageTemplate'teki değerleri kontrol et
        var renderedMessage = logEvent.RenderMessage();

        if (PanPattern().IsMatch(renderedMessage))
        {
            var scrubbed = PanPattern().Replace(renderedMessage, match =>
            {
                var pan = match.Value;
                if (pan.Length >= 13)
                    return $"{pan[..6]}{"".PadLeft(pan.Length - 10, '*')}{pan[^4..]}";
                return "****REDACTED****";
            });

            var property = propertyFactory.CreateProperty("SanitizedMessage", scrubbed);
            logEvent.AddPropertyIfAbsent(property);
        }
    }

    [GeneratedRegex(@"\b\d{13,19}\b")]
    private static partial Regex PanPattern();
}
```

- [ ] Tamamlandı

---

## ADIM 1.22 — Correlation ID Middleware

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Api\CorrelationIdMiddleware.cs`

```csharp
using Microsoft.AspNetCore.Http;

namespace MiniBank.Shared.Infrastructure.Api;

/// <summary>
/// Her HTTP isteğine benzersiz bir CorrelationId atar.
/// Bu ID tüm loglar, Kafka event'ler ve servisler arası isteklerde taşınır.
/// Distributed tracing için kritik: bir ödeme işlemi hangi servislerden geçti?
/// 
/// İstek header'da "X-Correlation-Id" varsa onu kullanır.
/// Yoksa yeni bir Guid üretir.
/// Response header'ına da ekler.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }
}
```

- [ ] Tamamlandı

---

## ADIM 1.23 — Cursor-Based Pagination

**DOSYA OLUŞTUR:** `src\Shared\MiniBank.Shared.Infrastructure\Api\PagedResponse.cs`

```csharp
namespace MiniBank.Shared.Infrastructure.Api;

/// <summary>
/// Cursor-based sayfalama response'u.
/// Bankacılıkta OFFSET kullanılmaz çünkü:
/// - Milyon satırda OFFSET yavaşlar (O(n))
/// - Eş zamanlı INSERT'lerde satır atlanır veya tekrarlanır
/// Cursor (son görülen ID) ile sorgu: WHERE id > @cursor ORDER BY id LIMIT @size
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore,
    int TotalCount
);
```

- [ ] Tamamlandı

---

## ADIM 1.24 — Infrastructure NuGet Paket Kontrolü ve Build

**KOMUTLAR:**

```powershell
# Infrastructure projesinin tüm paketlerini doğrula
dotnet list src\Shared\MiniBank.Shared.Infrastructure\MiniBank.Shared.Infrastructure.csproj package

# Tam build
dotnet build MiniBank.sln
# Beklenen: Build succeeded, 0 error, 0 warning
```

- [ ] Tamamlandı

---

## ADIM 1.25 — Tüm Testleri Çalıştır

**KOMUTLAR:**

```powershell
dotnet test MiniBank.sln --verbosity normal
# Beklenen:
# MoneyTests → 13 test geçer
# PanTests → 10 test geçer
# NationalIdTests → 6 test geçer
# IbanTests → 6 test geçer
# StronglyTypedIdTests → 5 test geçer
# SmokeTest → 1 test geçer
# TOPLAM: 41 test, hepsi "Passed"
```

- [ ] Tamamlandı

---

## ADIM 1.26 — Git Commit

**KOMUTLAR:**

```powershell
git add -A
git commit -m "Faz 1: Shared Kernel - value objects, exceptions, base entity, outbox, cache, middleware"
```

- [ ] Tamamlandı

---

## ✅ FAZ 1 KABUL KRİTERLERİ

```
□ Money value object: TRY + USD toplamak exception fırlatıyor
□ Pan value object: Luhn doğrulaması çalışıyor, ToString() maskelenmiş döner
□ Iban value object: MOD 97 doğrulaması çalışıyor
□ NationalId value object: TC Kimlik No algoritmik doğrulaması çalışıyor
□ CardExpiry value object: ay/yıl doğrulaması ve IsExpired çalışıyor
□ Strongly typed ID'ler: AccountId ve CardId birbiriyle karışmıyor
□ Entity<TId> ve AggregateRoot<TId> base class'ları var
□ BaseDbContext: Outbox tablosu konfigüre, SaveChanges'ta event'ler outbox'a yazılıyor
□ OutboxProcessor: Background service mesajları okuyup işliyor
□ RedisCacheService: Lua script ile atomik limit kontrolü çalışıyor
□ IdempotencyMiddleware: Aynı key ile gelen istek cached response dönüyor
□ GlobalExceptionHandler: DomainException → 422, diğer → 500 dönüyor
□ PanScrubber: Logda 16 haneli sayı maskeleniyor
□ CorrelationIdMiddleware: Header'dan CorrelationId alıyor veya üretiyor
□ Tüm testler geçiyor (41+)
□ dotnet build → 0 error, 0 warning
□ Git commit yapıldı
```

**Faz 1 tamamlandıysa, bu satırı işaretle:** ⬜ → ✅

---

*Faz 2 (IAM Service) ayrı dosyada: `IMPLEMENTATION_FAZ2.md`*
