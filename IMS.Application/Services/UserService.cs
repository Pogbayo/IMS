using IMS.Application.ApiResponse;
using IMS.Application.DTO.User;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Mailer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using IMS.Infrastructure.Token;

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
                    _logger.LogWarning("Company not found");
                }

                company!.Users.Add(user);

                await _context.SaveChangesAsync();

                _jobqueue.Enqueue<IAuditService>(job => job.LogAsync(
                   user.Id,
                   company.Id,
                   AuditAction.Create,
                   $"User {dto.Email} added.")
                );
               
                _logger.LogInformation("User {UserName} created successfully to the company", dto.UserName);

                _jobqueue.Enqueue<IMailerService>(job => job.SendEmailAsync(
                  user.Email,
                  $"You have been added to a company",
                  $"Hello,\n\nYou have been added to the company: {dto.CompanyId}. You can now log in using your credentials.")
               );

               _cache.Remove($"company:{dto.CompanyId}:users");

                var userdetails = new AddedUserResponseDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    LoginPassword = password
                };

                return Result<AddedUserResponseDto>.SuccessResponse(userdetails);
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

            if (user is null)
            {
                _logger.LogWarning("User not found.");
                return Result<LoginResponseDto>.FailureResponse("User with provided email address does not exist");
            }

            var token = await _tokenGenerator.GenerateAccessToken(user);

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Token generation failed");
                throw new ArgumentNullException("Token is null");
            }

            var newObject = new LoginResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    PhoneNumber = user.PhoneNumber!,
                    IsCompanyAdmin  = user.IsCompanyAdmin,
                    UserName = user.FirstName,
                    CompanyId = user.CompanyId
                },
            };

            _logger.LogInformation($"This is the user details,{user}");
            return Result<LoginResponseDto>.SuccessResponse(newObject, "Login successful.");
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

                _cache.Remove($"UserById{userId}");

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

                if (user == null)
                    return Result<string>.FailureResponse("User not found");

                user.CompanyId = null;
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;

                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;

                //user.TokenVersion += 1;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                    return Result<string>.FailureResponse(
                        string.Join("; ", updateResult.Errors.Select(e => e.Description))
                    );

                _jobqueue.Enqueue<IAuditService>(job => job.LogAsync(
                    user.Id,
                    companyId,
                    AuditAction.Delete,
                    $"User {user.Email} removed from company"
                ));

                _jobqueue.Enqueue<IMailerService>(job => job.SendEmailAsync(
                    user.Email!,
                    "Access Revoked",
                    "You have been removed from your company and can no longer log in."
                ));

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
        public async Task<Result<UserDto>> GetUserById(Guid userId)
        {
            var cacheKey = $"UserById{userId}";
            try
            {
                if (!_cache.TryGetValue(cacheKey,out UserDto cachedUser))
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

                    var options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(2),
                    };
                    _cache.Set(cacheKey, dto, options);

                    return Result<UserDto>.SuccessResponse(dto);
                }
                else
                {
                    return Result<UserDto>.SuccessResponse(cachedUser, "User fetched successfully from Cache system");
                }

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
            { 
                _logger.LogInformation("Company does not exist");
                return Result<List<UserDto>>.FailureResponse("CompanyId can not be null");
            }

            var company = await _context.Companies.FindAsync(companyId);

            if (company == null)
            {
                _logger.LogInformation("Company does not exist");
                return Result<List<UserDto>>.FailureResponse("Company does not exist");
            }

            var cacheKey = $"company:{companyId}:users";

            try
            {
                if (!_cache.TryGetValue<List<UserDto>>(cacheKey, out var cachedCompanyUsers))
                {
                    _logger.LogInformation("Data not found in the cache system, hitting the databse...");

                    var users = await _userManager.Users
                   .Where(u => u.CompanyId == companyId)
                   .Select(u => new UserDto
                   {
                       Id = u.Id,
                       UserName = u.UserName!,
                       Email = u.Email!
                   })
                   .ToListAsync();

                    var options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(2),
                    };
                    _cache.Set(cacheKey,users,options);

                    _logger.LogInformation("Data successfully retrieved from the database...");
                    return Result<List<UserDto>>.SuccessResponse(users);
                }
                else
                {
                    _logger.LogInformation("Successfully retrieved data from the Cached system");
                    return Result<List<UserDto>>.SuccessResponse(cachedCompanyUsers, "Data successfully retrieved from the Cached system");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users for company {CompanyId}", companyId);
                return Result<List<UserDto>>.FailureResponse("Failed to fetch users");
            }
        }
        public async Task<Result<string>> AddRoleToUser(Guid userId, string role)
        {
            if (userId == Guid.Empty)
            {
                _logger.LogInformation("UserId can not be null...");
                return Result<string>.FailureResponse("UserId can not be null");
            }

            if (string.IsNullOrEmpty(role))
            {
                _logger.LogInformation("URole can not be null");
                return Result<string>.FailureResponse("Please, provide a role...");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Result<string>.FailureResponse("User not found");

                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(role));

                var result = await _userManager.AddToRoleAsync(user, role);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await _audit.LogAsync(userId, Guid.Empty, AuditAction.Update, $"Role {role} added to user {user.Email}.");
                _logger.LogInformation("Role {Role} added to user {UserId}", role, userId);

                await _mailer.SendEmailAsync(user.Email!, "Role Updated!", $"Hello,\n\nYour role has been updated to: {role}.");

                _cache.Remove($"UserById{userId}");

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
            if (userId == Guid.Empty)
            {
                _logger.LogInformation("PUseId can not be null");
                return Result<string>.FailureResponse("UserId can not be null");
            }

            if (file == null || file.Length == 0 )
            {
                _logger.LogInformation("File can not be null...");
                return Result<string>.FailureResponse("Invalid file format...");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return Result<string>.FailureResponse("User not found");

                var UploadResult = await _imageService.UploadImageAsync(file, "user", userId);
                user.ImageUrl = UploadResult;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return Result<string>.FailureResponse(string.Join("; ", result.Errors.Select(e => e.Description)));

                await _audit.LogAsync(userId, Guid.Empty, AuditAction.Update, $"Profile image updated for user {user.Email}.");
                _logger.LogInformation("Profile image updated for user {UserId}", userId);

                _cache.Remove($"UserById{userId}");

                return Result<string>.SuccessResponse("Profile image updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile image for user {UserId}", userId);
                return Result<string>.FailureResponse("Failed to update profile image");
            }
        }
    }
}
