using System.Security.Cryptography;
using System.Text;

namespace MiniBank.Shared.Domain.ValueObjects;

public sealed class Pan : IEquatable<Pan>
{
    private readonly string _value;

    private Pan(string value) { _value = value; }

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

    public string Last4 => _value[^4..];
    public string Bin => _value[..6];
    public string Masked => $"{_value[..6]}{"".PadLeft(_value.Length - 10, '*')}{Last4}";

    public string Hashed
    {
        get
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_value));
            return Convert.ToHexStringLower(bytes);
        }
    }

    private static bool PassesLuhn(string number)
    {
        var sum = 0;
        var alternate = false;
        for (var i = number.Length - 1; i >= 0; i--)
        {
            var digit = number[i] - '0';
            if (alternate) { digit *= 2; if (digit > 9) digit -= 9; }
            sum += digit;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    public override string ToString() => Masked;
    public bool Equals(Pan? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => obj is Pan other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
}
