using IMS.Application.DTO.User;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace IMS.Application.Services
{
    public class UserService : IUserService
    {
        public Task<Guid> AddUserToCompany(CreateUserDto dto)
        {
            throw new NotImplementedException();
        }

        public Task DeleteUser(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<UserDto> GetUserById(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<List<UserDto>> GetUsersByCompany(Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<string> UpdateProfileImage(Guid userId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public Task UpdateUser(Guid userId, UpdateUserDto dto)
        {
            throw new NotImplementedException();
        }
    }
}
