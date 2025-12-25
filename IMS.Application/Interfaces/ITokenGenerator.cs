using IMS.Domain.Entities;
namespace IMS.Infrastructure.Token
{
    public interface ITokenGenerator
    {
        Task<string> GenerateAccessToken(AppUser user);
    }
}
