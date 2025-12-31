using IMS.Application.DTO.User;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : BaseController
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        //[Authorize(Policy = "AdminOnly")]
        [HttpPost("add-to-company")]
        public async Task<IActionResult> AddUserToCompany([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.AddUserToCompany(dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }

        //[Authorize(Policy = "AdminOnly")]
        [HttpGet("get-all-users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] Guid companyId)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.GetUsersByCompany(companyId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse("Error fetching users");
        }

        //[Authorize(Policy = "AdminOnly")]
        [HttpPut("update/{userId}")]
        public async Task<IActionResult> UpdateUser(
            [FromRoute] Guid userId,
            [FromBody] UpdateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.UpdateUser(userId, dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.Login(dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }

        //[Authorize(Policy = "AdminOnly")]
        [HttpDelete("delete/{userId}")]
        public async Task<IActionResult> DeleteUser([FromRoute] Guid userId)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.DeleteUser(userId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }

        //[Authorize(Policy = "AdminOnly")]
        [HttpPost("add-role")]
        public async Task<IActionResult> AddRoleToUser(
            [FromQuery] Guid userId,
            [FromQuery] string role)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.AddRoleToUser(userId, role);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpGet("by-id")]
        public async Task<IActionResult> GetById([FromQuery] Guid userId)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.GetUserById(userId);
            return result.Success
                ? OkResponse(result)
                : NotFoundResponse("User not found");
        }

        //[Authorize(Policy = "Everyone")]
        [HttpPut("update-profile-image")]
        public async Task<IActionResult> UpdateProfileImage(
            [FromQuery] Guid userId,
            IFormFile file)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (file == null || file.Length == 0)
                return BadRequest("Invalid file");

            var result = await _userService.UpdateProfileImage(userId, file);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }
    }
}
