using MiniBank.AuthorizationService.Api.Dtos;
using MiniBank.AuthorizationService.Domain.Entities;

namespace MiniBank.AuthorizationService.Application;

public interface IAuthorizationEngine
{
    Task<Authorization> AuthorizeAsync(AuthorizeRequest request, CancellationToken ct);
}
