using IMS.Application.ApiResponse;
using IMS.Application.DTO.User;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Mailer;
using IMS.Infrastructure.Token;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly ILogger<UserService> _logger;
        private readonly IAuditService _audit;
        private readonly IMailerService _mailer;
        private readonly ICustomMemoryCache _cache;
        private readonly ITokenGenerator _tokenGenerator;
        private readonly IAppDbContext _context;
        private readonly IImageService _imageService;
        private readonly IJobQueue _jobqueue;

        public UserService(
            IJobQueue jobqueue,
            ITokenGenerator tokenGenerator,
            IImageService imageService,
            ICustomMemoryCache cache,
            IMailerService mailer,
            IAppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            ILogger<UserService> logger,
            IAuditService audit)
        {
            _jobqueue = jobqueue;
            _mailer = mailer;
            _imageService = imageService;
            _context = context;
            _cache = cache;
            _tokenGenerator = tokenGenerator;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _audit = audit;
        }


        #region User Operations

        public async Task<Result<AddedUserResponseDto>> AddUserToCompany(CreateUserDto dto)
        {
            try
            {
                var user = new AppUser
                {
                    UserName = dto.UserName,
                    Email = dto.Email
                };

                var password = $"{dto.FirstName}123$";

                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                    return Result<AddedUserResponseDto>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await AddRoleToUser(user.Id, dto.UserRole.ToString());

                var company = await _context.Companies
                    .Where(c => c.Id == dto.CompanyId)
                    .FirstOrDefaultAsync();

                if (company == null)
                {
                    _logger.LogWarning("Company not found for Id {CompanyId}", dto.CompanyId);
                    return Result<AddedUserResponseDto>.FailureResponse("Company not found");
                }

                company.Users.Add(user);
                await _context.SaveChangesAsync();

                _cache.Remove($"company:{dto.CompanyId}:users");

                _jobqueue.EnqueueAudit(user.Id, dto.CompanyId, AuditAction.Create, $"User {user.Email} added to company.");
                _jobqueue.EnqueueEmail(user.Email, "You have been added to a company",
                    $"Hello,\n\nYou have been added to the company: {dto.CompanyId}. You can now log in using your credentials.");
                _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} ({user.Email}) added to Company {dto.CompanyId}.");

                _logger.LogInformation("User {UserName} created successfully for company {CompanyId}", dto.UserName, dto.CompanyId);

                return Result<AddedUserResponseDto>.SuccessResponse(new AddedUserResponseDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    LoginPassword = password
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {Email}", dto.Email);
                return Result<AddedUserResponseDto>.FailureResponse("Failed to add user");
            }
        }

        public async Task<Result<LoginResponseDto>> Login(LoginUserDto userDetails)
        {
            if (string.IsNullOrWhiteSpace(userDetails.Email) || string.IsNullOrWhiteSpace(userDetails.Password))
            {
                _logger.LogWarning("Invalid credentials provided.");
                return Result<LoginResponseDto>.FailureResponse("Invalid credentials.", "Email and Password cannot be empty.");
            }

            var user = await _userManager.FindByEmailAsync(userDetails.Email);
            if (user == null)
            {
                _logger.LogWarning("User not found for email {Email}", userDetails.Email);
                return Result<LoginResponseDto>.FailureResponse("User with provided email address does not exist");
            }

            var token = await _tokenGenerator.GenerateAccessToken(user);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Token generation failed for user {UserId}", user.Id);
                throw new ArgumentNullException("Token is null");
            }

            if (!user.CompanyId.HasValue)
                return Result<LoginResponseDto>.FailureResponse("User has no company");

            var response = new LoginResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    PhoneNumber = user.PhoneNumber!,
                    IsCompanyAdmin = user.IsCompanyAdmin,
                    UserName = user.FirstName,
                    CompanyId = user.CompanyId
                }
            };

            _logger.LogInformation("User {UserId} logged in", user.Id);

            _jobqueue.EnqueueAudit(user.Id, user.CompanyId!.Value, AuditAction.Login, $"{user.FirstName} logged in.");
            _jobqueue.EnqueueEmail(user.Email!, $"Welcome back {user.FirstName}",
                $"Hello {user.FirstName},\n\nWe're happy to see you back! You can now continue using your account.\n\nBest regards,\nYour Company Team");
            _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} ({user.Email}) logged in to Company {user.CompanyId}.");

            return Result<LoginResponseDto>.SuccessResponse(response, "Login successful.");
        }

        public async Task<Result<string>> SendConfirmationLink(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return Result<string>.FailureResponse("User not found");

            if (user.EmailConfirmed)
                return Result<string>.FailureResponse("Email already confirmed");

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = Uri.EscapeDataString(token);

            var confirmUrl = $"https://frontend.com/confirm-email?userId={user.Id}&token={encodedToken}";

            _jobqueue.EnqueueEmail(user.Email!, "Confirm your email",
                $"Hello {user.FirstName},\n\nPlease confirm your email by clicking the link below:\n{confirmUrl}\n\nIf you did not request this, please ignore this email.");
            _jobqueue.EnqueueAudit(user.Id, user.CompanyId!.Value, AuditAction.Create, "Email confirmation link sent");
            _jobqueue.EnqueueCloudWatchAudit($"Confirmation link sent to user {user.Id} ({user.Email}) for Company {user.CompanyId}");

            return Result<string>.SuccessResponse("Confirmation email sent");
        }

        public async Task<Result<string>> ConfirmEmail(Guid userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return Result<string>.FailureResponse("User not found");

            if (user.EmailConfirmed)
                return Result<string>.FailureResponse("Email already confirmed");

            var decodedToken = Uri.UnescapeDataString(token);
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (!result.Succeeded)
                return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

            user.EmailConfirmed = true;
            await _context.SaveChangesAsync();

            _jobqueue.EnqueueAudit(user.Id, user.CompanyId!.Value, AuditAction.Update, "Email confirmed");
            _jobqueue.EnqueueEmail(user.Email!, "Email confirmed",
                $"Hello {user.FirstName},\n\nYour email has been successfully confirmed.\nYou can now access your dashboard.");
            _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} ({user.Email}) confirmed email for Company {user.CompanyId}");

            return Result<string>.SuccessResponse("Email confirmed successfully");
        }


        public async Task<Result<string>> UpdateUser(Guid userId, UpdateUserDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null) return Result<string>.FailureResponse("User not found");

                user.Email = dto.Email ?? user.Email;
                user.UserName = dto.UserName ?? user.UserName;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                _jobqueue.EnqueueAudit(userId, user.CompanyId!.Value, AuditAction.Update, $"User {user.Email} updated.");
                _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} ({user.Email}) updated in Company {user.CompanyId}");

                _cache.Remove($"UserById{userId}");
                _logger.LogInformation("User {UserId} updated", userId);

                return Result<string>.SuccessResponse("User updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", userId);
                return Result<string>.FailureResponse("Failed to update user");
            }
        }

        public async Task<Result<string>> RemoveUserFromCompany(Guid userId, Guid companyId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null) return Result<string>.FailureResponse("User not found");

                user.CompanyId = null;
                user.MarkAsDeleted();
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                user.Tokenversion += 1;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                _jobqueue.EnqueueAudit(user.Id, companyId, AuditAction.Invalidate, $"User {user.Email} removed from company");
                _jobqueue.EnqueueEmail(user.Email!, "Access Revoked", "You have been removed from your company and can no longer log in.");
                _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} ({user.Email}) removed from Company {companyId}");

                _cache.Remove($"UserById{userId}");
                _cache.Remove($"company:{companyId}:users");

                _logger.LogInformation("User {UserId} removed from company {CompanyId}", userId, companyId);
                return Result<string>.SuccessResponse("User removed from company successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId}", userId);
                return Result<string>.FailureResponse("Failed to remove user from company");
            }
        }

        public async Task<Result<string>> Logout(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return Result<string>.FailureResponse("User not found");

            user.Tokenversion += 1;
            await _userManager.UpdateAsync(user);

            _jobqueue.EnqueueAudit(user.Id, user.CompanyId!.Value, AuditAction.Logout, "User logged out");
            _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} ({user.Email}) logged out from Company {user.CompanyId}");

            _logger.LogInformation("User {UserId} logged out", userId);

            return Result<string>.SuccessResponse("Logged out successfully");
        }

        public async Task<Result<UserDto>> GetUserById(Guid userId)
        {
            var cacheKey = $"UserById{userId}";
            try
            {
                if (!_cache.TryGetValue(cacheKey, out UserDto cachedUser))
                {
                    var user = await _userManager.FindByIdAsync(userId.ToString());
                    if (user == null) return Result<UserDto>.FailureResponse("User not found");

                    var dto = new UserDto { Id = user.Id, UserName = user.UserName!, Email = user.Email! };
                    _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });

                    _jobqueue.EnqueueAudit(user.Id, user.CompanyId!.Value, AuditAction.Read, $"Fetched user {user.Email} by ID");
                    _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} ({user.Email}) fetched by ID from Company {user.CompanyId}");

                    return Result<UserDto>.SuccessResponse(dto);
                }

                return Result<UserDto>.SuccessResponse(cachedUser, "User fetched successfully from cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user {UserId}", userId);
                return Result<UserDto>.FailureResponse("Failed to fetch user");
            }
        }

        public async Task<Result<List<UserDto>>> GetUsersByCompany(Guid companyId)
        {
            if (companyId == Guid.Empty)
                return Result<List<UserDto>>.FailureResponse("CompanyId cannot be null");

            var company = await _context.Companies.FindAsync(companyId);
            if (company == null) return Result<List<UserDto>>.FailureResponse("Company does not exist");

            var cacheKey = $"company:{companyId}:users";
            try
            {
                if (!_cache.TryGetValue(cacheKey, out List<UserDto> cachedUsers))
                {
                    var users = await _userManager.Users
                        .Where(u => u.CompanyId == companyId)
                        .Select(u => new UserDto { Id = u.Id, UserName = u.UserName!, Email = u.Email! })
                        .ToListAsync();

                    _cache.Set(cacheKey, users, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });

                    _jobqueue.EnqueueAudit(Guid.Empty, companyId, AuditAction.Read, $"Fetched {users.Count} users for company {companyId}");
                    _jobqueue.EnqueueCloudWatchAudit($"Fetched {users.Count} users for Company {companyId}");

                    return Result<List<UserDto>>.SuccessResponse(users);
                }

                return Result<List<UserDto>>.SuccessResponse(cachedUsers, "Data retrieved from cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users for company {CompanyId}", companyId);
                _jobqueue.EnqueueAudit(Guid.Empty, companyId, AuditAction.Error, $"Error fetching users for company {companyId}: {ex.Message}");
                _jobqueue.EnqueueCloudWatchAudit($"Error fetching users for Company {companyId}: {ex.Message}");
                return Result<List<UserDto>>.FailureResponse("Failed to fetch users");
            }
        }

        public async Task<Result<string>> AddRoleToUser(Guid userId, string role)
        {
            if (userId == Guid.Empty) return Result<string>.FailureResponse("UserId cannot be null");
            if (string.IsNullOrEmpty(role)) return Result<string>.FailureResponse("Role cannot be null");

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return Result<string>.FailureResponse("User not found");

            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));

            var result = await _userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
                return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

            _jobqueue.EnqueueAudit(userId, user.CompanyId!.Value, AuditAction.Update, $"Role {role} added to user {user.Email}.");
            _jobqueue.EnqueueEmail(user.Email!, "Role Updated!", $"Hello,\n\nYour role has been updated to: {role}.");
            _jobqueue.EnqueueCloudWatchAudit($"Role {role} added to User {user.Id} ({user.Email}) in Company {user.CompanyId}");

            _cache.Remove($"UserById{userId}");
            _logger.LogInformation("Role {Role} added to user {UserId}", role, userId);

            return Result<string>.SuccessResponse("Role added successfully");
        }

        public async Task<Result<string>> UpdateProfileImage(Guid userId, IFormFile file)
        {
            if (userId == Guid.Empty) return Result<string>.FailureResponse("UserId cannot be null");
            if (file == null || file.Length == 0) return Result<string>.FailureResponse("Invalid file");

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return Result<string>.FailureResponse("User not found");

            var uploadResult = await _imageService.UploadImageAsync(file, "user", userId);
            user.ImageUrl = uploadResult;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

            _jobqueue.EnqueueAudit(userId, user.CompanyId!.Value, AuditAction.Update, $"Profile image updated for user {user.Email}.");
            _jobqueue.EnqueueCloudWatchAudit($"Profile image updated for User {user.Id} ({user.Email}) in Company {user.CompanyId}");

            _cache.Remove($"UserById{userId}");
            _logger.LogInformation("Profile image updated for user {UserId}", userId);

            return Result<string>.SuccessResponse("Profile image updated successfully");
        }

        public async Task<Result<string>> UpdatePassword(Guid userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return Result<string>.FailureResponse("User not found");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
                return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

            _jobqueue.EnqueueAudit(user.Id, user.CompanyId!.Value, AuditAction.Update, "Password updated successfully");
            _jobqueue.EnqueueEmail(user.Email!, "Password Updated", "Hello,\n\nYour account password has been successfully updated.");
            _jobqueue.EnqueueCloudWatchAudit($"Password updated for User {user.Id} ({user.Email}) in Company {user.CompanyId}");

            _cache.Remove($"UserById{userId}");
            _logger.LogInformation("Password updated for user {UserId}", userId);

            return Result<string>.SuccessResponse("Password updated successfully");
        }

    }
}
#endregion