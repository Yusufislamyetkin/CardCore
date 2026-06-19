using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.IamService.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; } = default!;
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByToken { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired(DateTime utcNow) => ExpiresAt < utcNow;
    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    private RefreshToken() { }

    public static RefreshToken Create(UserId userId, string token, DateTime expiresAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            RevokedAt = null,
            ReplacedByToken = null
        };

    public void Revoke(DateTime utcNow, string? replacedByToken = null)
    {
        RevokedAt = utcNow;
        ReplacedByToken = replacedByToken;
    }
}
