using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.IamService.Api.Dtos;
using MiniBank.IamService.Application;
using IamUser = MiniBank.IamService.Domain.Entities.User;
using MiniBank.IamService.Domain.Entities;
using MiniBank.IamService.Domain.Enums;
using MiniBank.IamService.Infrastructure.Persistence;

namespace MiniBank.IamService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IamDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IamDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { error = "Email already registered" });

        // Self-registration always creates a customer; elevated roles must not be self-assigned
        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = IamUser.Create(email, passwordHash, Role.Customer);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, new { id = user.Id.Value, email = user.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var utcNow = DateTime.UtcNow;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return Unauthorized(new { error = "Invalid credentials" });

        if (!user.IsActive)
            return Unauthorized(new { error = "Account is deactivated" });

        if (user.IsLockedOut(utcNow))
            return Unauthorized(new { error = "Account is temporarily locked", lockedUntil = user.LockedUntil });

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin(utcNow);
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = "Invalid credentials" });
        }

        user.RecordSuccessfulLogin();

        var refreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(user.Id, refreshTokenValue, utcNow.Add(RefreshTokenLifetime));
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        return Ok(new AuthResponse(
            accessToken,
            refreshTokenValue,
            utcNow.AddMinutes(15),
            user.Email,
            user.Role.ToString()));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;

        var existingToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, ct);

        if (existingToken is null || !existingToken.IsActive(utcNow))
            return Unauthorized(new { error = "Invalid or expired refresh token" });

        var user = await _db.Users.FindAsync(existingToken.UserId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "User not found or inactive" });

        var newRefreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        existingToken.Revoke(utcNow, newRefreshTokenValue);

        var newRefreshToken = RefreshToken.Create(user.Id, newRefreshTokenValue, utcNow.Add(RefreshTokenLifetime));
        _db.RefreshTokens.Add(newRefreshToken);

        await _db.SaveChangesAsync(ct);

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        return Ok(new AuthResponse(
            accessToken,
            newRefreshTokenValue,
            utcNow.AddMinutes(15),
            user.Email,
            user.Role.ToString()));
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;

        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, ct);

        if (token is null)
            return NotFound(new { error = "Token not found" });

        if (!token.IsRevoked)
        {
            token.Revoke(utcNow);
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }
}
