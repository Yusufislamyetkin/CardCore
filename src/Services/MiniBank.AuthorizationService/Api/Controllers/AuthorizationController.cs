using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.AuthorizationService.Api.Dtos;
using MiniBank.AuthorizationService.Application;
using MiniBank.AuthorizationService.Domain.Entities;
using MiniBank.AuthorizationService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.AuthorizationService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/authorizations")]
public sealed class AuthorizationController : ControllerBase
{
    private readonly AuthorizationDbContext _db;
    private readonly IAuthorizationEngine _engine;

    public AuthorizationController(AuthorizationDbContext db, IAuthorizationEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    [HttpPost]
    public async Task<IActionResult> Authorize([FromBody] AuthorizeRequest request, CancellationToken ct)
    {
        var authorization = await _engine.AuthorizeAsync(request, ct);
        _db.Authorizations.Add(authorization);
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(authorization));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var auth = await _db.Authorizations.FindAsync(AuthorizationId.From(id), ct);
        return auth is null ? NotFound() : Ok(ToResponse(auth));
    }

    [HttpGet("card/{cardId:guid}")]
    public async Task<IActionResult> GetByCard(Guid cardId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 20;
        var auths = await _db.Authorizations
            .Where(a => a.CardId == CardId.From(cardId))
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(auths.Select(ToResponse));
    }

    [HttpPost("{id:guid}/reverse")]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReverseRequest request, CancellationToken ct)
    {
        var auth = await _db.Authorizations.FindAsync(AuthorizationId.From(id), ct);
        if (auth is null) return NotFound();

        auth.Reverse(request.Reason);
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(auth));
    }

    private static AuthorizationResponse ToResponse(Authorization a) => new(
        a.Id.Value,
        a.Status == AuthorizationStatus.Approved,
        a.AuthorizationCode,
        a.Status == AuthorizationStatus.Declined ? a.DeclineReason.ToString() : null,
        a.Amount.Amount,
        a.Amount.Currency.ToString(),
        a.CreatedAt);
}
