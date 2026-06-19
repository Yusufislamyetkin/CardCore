using MiniBank.IamService.Domain.Entities;

namespace MiniBank.IamService.Application;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
