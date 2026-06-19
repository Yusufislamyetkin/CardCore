namespace MiniBank.Shared.Domain.ValueObjects;

public sealed record CardExpiry
{
    public int Month { get; }
    public int Year { get; }

    private CardExpiry(int month, int year) { Month = month; Year = year; }

    public static CardExpiry Create(int month, int year)
    {
        if (month < 1 || month > 12)
            throw new ArgumentException("Month must be 1-12.", nameof(month));
        var fullYear = year < 100 ? 2000 + year : year;
        if (fullYear < 2020 || fullYear > 2099)
            throw new ArgumentException("Year must be between 2020 and 2099.", nameof(year));
        return new CardExpiry(month, fullYear);
    }

    public static CardExpiry Parse(string expiry)
    {
        if (string.IsNullOrWhiteSpace(expiry))
            throw new ArgumentException("Expiry cannot be empty.", nameof(expiry));
        var cleaned = expiry.Replace("/", "").Replace("-", "");
        if (cleaned.Length != 4 || !cleaned.All(char.IsDigit))
            throw new ArgumentException("Expiry must be 4 digits (MMYY).", nameof(expiry));
        return Create(int.Parse(cleaned[..2]), int.Parse(cleaned[2..]));
    }

    public bool IsExpired(DateTime utcNow)
    {
        var expiryDate = new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month), 23, 59, 59);
        return utcNow > expiryDate;
    }

    public override string ToString() => $"{Month:D2}/{Year % 100:D2}";
}
