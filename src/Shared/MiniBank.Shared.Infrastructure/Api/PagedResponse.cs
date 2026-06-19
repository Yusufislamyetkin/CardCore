namespace MiniBank.Shared.Infrastructure.Api;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore,
    int TotalCount
);
