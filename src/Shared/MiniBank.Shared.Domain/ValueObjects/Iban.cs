using System.Numerics;
using System.Text.RegularExpressions;

namespace MiniBank.Shared.Domain.ValueObjects;

public sealed partial class Iban : IEquatable<Iban>
{
    private readonly string _value;

    private Iban(string value) { _value = value; }

    public static Iban Create(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            throw new ArgumentException("IBAN cannot be empty.", nameof(iban));

        var cleaned = iban.Replace(" ", "").ToUpperInvariant();

        if (!IbanPattern().IsMatch(cleaned))
            throw new ArgumentException($"Invalid IBAN format.", nameof(iban));

        if (!PassesMod97(cleaned))
            throw new ArgumentException($"IBAN failed MOD 97 check.", nameof(iban));

        return new Iban(cleaned);
    }

    public string Value => _value;
    public string BankCode => _value.Substring(4, Math.Min(5, _value.Length - 4));

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

    private static bool PassesMod97(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var sb = new System.Text.StringBuilder();
        foreach (var c in rearranged)
            sb.Append(char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString());
        return BigInteger.Parse(sb.ToString()) % 97 == 1;
    }

    public override string ToString() => Formatted;

    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{4,30}$")]
    private static partial Regex IbanPattern();

    public bool Equals(Iban? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => obj is Iban other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
}
