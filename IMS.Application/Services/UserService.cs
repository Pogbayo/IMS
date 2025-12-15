using IMS.Application.ApiResponse;
using IMS.Application.DTO.User;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace IMS.Application.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserService> _logger;
        private readonly IAuditService _audit;

        public UserService(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserService> logger,
            IAuditService audit)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _audit = audit;
        }

        public async Task<Result<Guid>> AddUserToCompany(CreateUserDto dto)
        {
            try
            {
                var user = new AppUser
                {
                    UserName = dto.UserName,
                    Email = dto.Email
                };

                var result = await _userManager.CreateAsync(user, dto.Password);

                if (!result.Succeeded)
                    return Result<Guid>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await _audit.LogAsync(Guid.NewGuid(), dto.CompanyId, AuditAction.Create, $"User {dto.Email} added.");

                _logger.LogInformation("User {UserName} created successfully", dto.UserName);

                await SendEmail(dto.Email, $"You have been added to a company", $"Hello,\n\nYou have been added to the company: {dto.CompanyId}. You can now log in using your credentials.");

                return Result<Guid>.SuccessResponse(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {Email}", dto.Email);
                return Result<Guid>.FailureResponse("Failed to add user");
            }
        }

        public async Task<Result<string>> UpdateUser(Guid userId, UpdateUserDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Result<string>.FailureResponse("User not found");

                user.Email = dto.Email ?? user.Email;
                user.UserName = dto.UserName ?? user.UserName;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await _audit.LogAsync(userId, Guid.Empty, AuditAction.Update, $"User {user.Email} updated.");
                _logger.LogInformation("User {UserId} updated", userId);

                return Result<string>.SuccessResponse("User updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", userId);
                return Result<string>.FailureResponse("Failed to update user");
            }
        }

        public async Task<Result<string>> DeleteUser(Guid userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Result<string>.FailureResponse("User not found");

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await _audit.LogAsync(userId, Guid.Empty, AuditAction.Delete, $"User {user.Email} deleted.");
                _logger.LogInformation("User {UserId} deleted", userId);

                await SendEmail(user.Email!, "Removed from Company", $"Hello,\n\nYou have been removed from your company and no longer have access.");

                return Result<string>.SuccessResponse("User deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return Result<string>.FailureResponse("Failed to delete user");
            }
        }

        public async Task<Result<UserDto>> GetUserById(Guid userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Result<UserDto>.FailureResponse("User not found");

                var dto = new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName!,
                    Email = user.Email!
                };

                return Result<UserDto>.SuccessResponse(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user {UserId}", userId);
                return Result<UserDto>.FailureResponse("Failed to fetch user");
            }
        }

        public async Task<Result<List<UserDto>>> GetUsersByCompany(Guid companyId)
        {
            try
            {
                var users =  await _userManager.Users
                    .Where(u => u.CompanyId == companyId)  
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        UserName = u.UserName!,
                        Email = u.Email!
                    })
                    .ToListAsync();

                return Result<List<UserDto>>.SuccessResponse(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users for company {CompanyId}", companyId);
                return Result<List<UserDto>>.FailureResponse("Failed to fetch users");
            }
        }

        public async Task<Result<string>> AddRoleToUser(Guid userId, string role)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Result<string>.FailureResponse("User not found");

                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));

                var result = await _userManager.AddToRoleAsync(user, role);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await _audit.LogAsync(userId, Guid.Empty, AuditAction.Update, $"Role {role} added to user {user.Email}.");
                _logger.LogInformation("Role {Role} added to user {UserId}", role, userId);

                await SendEmail(user.Email!, "Role Updated", $"Hello,\n\nYour role has been updated to: {role}.");

                return Result<string>.SuccessResponse("Role added successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding role to user {UserId}", userId);
                return Result<string>.FailureResponse("Failed to add role");
            }
        }

        public async Task<Result<string>> UpdateProfileImage(Guid userId, IFormFile file)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Result<string>.FailureResponse("User not found");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                user.GetType().GetProperty("ProfileImage")?.SetValue(user, stream.ToArray());

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await _audit.LogAsync(userId, Guid.Empty, AuditAction.Update, $"Profile image updated for user {user.Email}.");
                _logger.LogInformation("Profile image updated for user {UserId}", userId);

                return Result<string>.SuccessResponse("Profile image updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile image for user {UserId}", userId);
                return Result<string>.FailureResponse("Failed to update profile image");
            }
        }

        private async Task SendEmail(string email, string subject, string body)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.your-email-provider.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("your-email@example.com", "your-email-password"),
                    EnableSsl = true,
                };

                var message = new MailMessage
                {
                    From = new MailAddress("your-email@example.com", "IMS System"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                message.To.Add(email);

                await smtpClient.SendMailAsync(message);

                _logger.LogInformation("Email sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", email);
            }
        }
    }
}
