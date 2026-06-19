namespace MiniBank.IamService.Api.Dtos;

public sealed record RegisterRequest(string Email, string Password, string? Role = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Email,
    string Role);
