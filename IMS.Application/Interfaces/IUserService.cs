using IMS.Application.DTO.User;
using Microsoft.AspNetCore.Http;

namespace IMS.Application.Interfaces
{
    public interface IUserService
    {
        Task<Guid> AddUserToCompany(CreateUserDto dto);
        Task UpdateUser(Guid userId, UpdateUserDto dto);
        Task DeleteUser(Guid userId);
        Task<UserDto> GetUserById(Guid userId);
        Task<List<UserDto>> GetUsersByCompany(Guid companyId);
        Task<string> UpdateProfileImage(Guid userId, IFormFile file);
    }
}
