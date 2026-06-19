using MiniBank.AuthorizationService.Api.Dtos;
using MiniBank.AuthorizationService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using StackExchange.Redis;

namespace MiniBank.AuthorizationService.Application;

public sealed class AuthorizationEngine : IAuthorizationEngine
{
    // Atomically increments spending; returns new total or -1 if limit would be exceeded.
    // KEYS[1]: spend counter key, ARGV[1]: amount (cents), ARGV[2]: limit (cents), ARGV[3]: TTL seconds
    private const string IncrementIfBelowLimitLua = """
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        local newVal = current + tonumber(ARGV[1])
        if newVal <= tonumber(ARGV[2]) then
            redis.call('SET', KEYS[1], newVal, 'EX', tonumber(ARGV[3]))
            return newVal
        else
            return -1
        end
        """;

    private readonly IConnectionMultiplexer _redis;

    public AuthorizationEngine(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<Authorization> AuthorizeAsync(AuthorizeRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.CardId, out var cardGuid) ||
            !Guid.TryParse(request.AccountId, out var accountGuid))
            throw new ArgumentException("Invalid CardId or AccountId");

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            throw new ArgumentException($"Invalid currency: {request.Currency}");

        var cardId = CardId.From(cardGuid);
        var accountId = AccountId.From(accountGuid);
        var amount = new Money(request.Amount, currency);

        // 1. Card status check
        if (!string.Equals(request.CardStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var cardReason = request.CardStatus.Equals("Expired", StringComparison.OrdinalIgnoreCase)
                ? DeclineReason.CardExpired
                : DeclineReason.CardNotActive;
            return Authorization.Decline(cardId, accountId, request.PanHash, amount,
                request.MerchantId, request.MerchantName, cardReason);
        }

        // 2. Card expiry check
        try
        {
            var expiry = CardExpiry.Parse(request.CardExpiry);
            if (expiry.IsExpired(DateTime.UtcNow))
                return Authorization.Decline(cardId, accountId, request.PanHash, amount,
                    request.MerchantId, request.MerchantName, DeclineReason.CardExpired);
        }
        catch
        {
            return Authorization.Decline(cardId, accountId, request.PanHash, amount,
                request.MerchantId, request.MerchantName, DeclineReason.CardExpired);
        }

        // 3. Balance check
        if (request.AvailableBalance < request.Amount)
            return Authorization.Decline(cardId, accountId, request.PanHash, amount,
                request.MerchantId, request.MerchantName, DeclineReason.InsufficientFunds);

        var db = _redis.GetDatabase();
        var amountCents = (long)(request.Amount * 100);

        // 4. Daily limit check — atomic increment-if-below-limit Lua script
        if (request.DailyLimit.HasValue)
        {
            var dailyKey = (RedisKey)$"spend:daily:{request.CardId}:{DateTime.UtcNow:yyyyMMdd}";
            var limitCents = (long)(request.DailyLimit.Value * 100);
            var ttlSeconds = (long)TimeSpan.FromDays(2).TotalSeconds;

            var result = (long)await db.ScriptEvaluateAsync(
                IncrementIfBelowLimitLua,
                [dailyKey],
                [(RedisValue)amountCents, (RedisValue)limitCents, (RedisValue)ttlSeconds]);

            if (result < 0)
                return Authorization.Decline(cardId, accountId, request.PanHash, amount,
                    request.MerchantId, request.MerchantName, DeclineReason.LimitExceeded);
        }

        // 5. Monthly limit check
        if (request.MonthlyLimit.HasValue)
        {
            var monthlyKey = (RedisKey)$"spend:monthly:{request.CardId}:{DateTime.UtcNow:yyyyMM}";
            var limitCents = (long)(request.MonthlyLimit.Value * 100);
            var ttlSeconds = (long)TimeSpan.FromDays(35).TotalSeconds;

            var result = (long)await db.ScriptEvaluateAsync(
                IncrementIfBelowLimitLua,
                [monthlyKey],
                [(RedisValue)amountCents, (RedisValue)limitCents, (RedisValue)ttlSeconds]);

            if (result < 0)
            {
                // Roll back daily spend on monthly limit failure
                if (request.DailyLimit.HasValue)
                {
                    var dailyKey = (RedisKey)$"spend:daily:{request.CardId}:{DateTime.UtcNow:yyyyMMdd}";
                    await db.StringDecrementAsync(dailyKey, amountCents);
                }
                return Authorization.Decline(cardId, accountId, request.PanHash, amount,
                    request.MerchantId, request.MerchantName, DeclineReason.LimitExceeded);
            }
        }

        return Authorization.Approve(cardId, accountId, request.PanHash, amount,
            request.MerchantId, request.MerchantName);
    }
}
