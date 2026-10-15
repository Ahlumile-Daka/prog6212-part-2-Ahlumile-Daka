using RaceDay.Api.Models;

namespace RaceDay.Api.Services
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}
