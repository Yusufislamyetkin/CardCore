namespace MiniBank.Shared.Domain.ValueObjects;

public sealed class NationalId : IEquatable<NationalId>
{
    private readonly string _value;

    private NationalId(string value) { _value = value; }

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
        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        var tenthDigit = (oddSum * 7 - evenSum) % 10;
        if (tenthDigit < 0) tenthDigit += 10;

        if (digits[9] != tenthDigit)
            throw new ArgumentException("National ID checksum invalid (digit 10).", nameof(nationalId));

        if (digits[10] != digits.Take(10).Sum() % 10)
            throw new ArgumentException("National ID checksum invalid (digit 11).", nameof(nationalId));

        return new NationalId(cleaned);
    }

    public string Value => _value;
    public string Masked => $"*******{_value[^4..]}";
    public override string ToString() => Masked;
    public bool Equals(NationalId? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => obj is NationalId other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
}
