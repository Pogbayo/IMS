using IMS.Application.DTO.User;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UserController : BaseController
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [Authorize(Policy = "AdminOnly")]
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

        [Authorize(Policy = "AdminOnly")]
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

        [Authorize(Policy = "AdminOnly")]
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

        [AllowAnonymous]
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

        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("delete/{userId}")]
        public async Task<IActionResult> DeleteUser([FromRoute] Guid userId, [FromRoute] Guid companyId)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.RemoveUserFromCompany(userId,companyId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }

        [Authorize(Policy = "AdminOnly")]
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

        [Authorize(Policy ="AdminOnly")]
        [HttpPost("logout/{userId}")]
        public async Task<IActionResult> Logout([FromRoute] Guid userId)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.Logout(userId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }


        [Authorize(Policy = "Everyone")]
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

        [Authorize(Policy = "Everyone")]
        [Consumes("multipart/form-data")]
        [HttpPut("update-profile-image")]
        public async Task<IActionResult> UpdateProfileImage(
        [FromForm] UpdateProfileImageRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Invalid file");

            var result = await _userService.UpdateProfileImage(
                request.UserId,
                request.File
            );

            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!);
        }



        [Authorize]
        [HttpPost("send-confirmation-email")]
        public async Task<IActionResult> SendConfirmationEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required");

            var result = await _userService.SendConfirmationLink(email);

            return result.Success
                ? Ok(result)
                : BadRequest(result);
        }

        [AllowAnonymous]
        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(
            [FromQuery] Guid userId,
            [FromQuery] string token)
        {
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
                return BadRequest("Invalid confirmation data");

            var result = await _userService.ConfirmEmail(userId, token);

            return result.Success
                ? Ok(result)
                : BadRequest(result);
        }
    }
}
