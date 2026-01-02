using IMS.Application.ApiResponse;
using IMS.Application.DTO.User;
using Microsoft.AspNetCore.Http;

namespace IMS.Application.Interfaces
{
    public interface IUserService
    {
        Task<Result<AddedUserResponseDto>> AddUserToCompany(CreateUserDto dto);
        Task<Result<string>> UpdateUser(Guid userId, UpdateUserDto dto);
        Task<Result<string>> RemoveUserFromCompany(Guid userId, Guid companyId);
        Task<Result<UserDto>> GetUserById(Guid userId);
        Task<Result<List<UserDto>>> GetUsersByCompany(Guid companyId);
        Task<Result<string>> AddRoleToUser(Guid userId, string role);
        Task<Result<string>> UpdateProfileImage(Guid userId, IFormFile file);
        Task<Result<LoginResponseDto>> Login(LoginUserDto userdetails);
    }
}
