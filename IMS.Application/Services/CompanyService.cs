using IMS.Application.ApiResponse;
using IMS.Application.DTO.Company;
using IMS.Application.DTO.Product;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Infrastructure.Mailer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ILogger<CompanyService> _logger;
        private readonly IAppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _audit;
        private readonly IMailerService _mailer;
        private readonly ICurrentUserService _currentUserService;
        public CompanyService(
            ILogger<CompanyService> logger,
            IAppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService audit,
            IMailerService mailer,
            ICurrentUserService currentUserService
            )
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _audit = audit;
            _mailer = mailer;
            _currentUserService = currentUserService;
        }
        public async Task<Result<CompanyDto>> RegisterCompanyAndAdmin(CompanyCreateDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto), "Incomplete details -- Company details are required.");
            }

            if (string.IsNullOrWhiteSpace(dto.CompanyName))
                throw new ArgumentNullException(nameof(dto.CompanyName));

            if (string.IsNullOrWhiteSpace(dto.CompanyEmail))
                throw new ArgumentNullException(nameof(dto.CompanyEmail));

            if (string.IsNullOrWhiteSpace(dto.HeadOffice))
                throw new ArgumentNullException(nameof(dto.HeadOffice));

            var company = new Company
            {
                Name = dto.CompanyName,
                Email = dto.CompanyEmail,
                HeadOffice = dto.HeadOffice
            };

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            var AppUser = new AppUser
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Company = company,
                CreatedCompany = company,
                IsCompanyAdmin = true
            };

            var IdentityResult = await _userManager.CreateAsync(AppUser, dto.Password);

            if (!IdentityResult.Succeeded)
            {
                var errorMessages = string.Join("; ", IdentityResult.Errors.Select(e => e.Description));
                return Result<CompanyDto>.FailureResponse($"Error registering user: {errorMessages}");
            }

            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            await _userManager.AddToRoleAsync(AppUser, "Admin");

            company.Users.Add(AppUser);
            company.CreatedById = AppUser.Id;

            try
            {
                await _mailer.SendEmailAsync(
                    toEmail: AppUser.Email!,
                    subject: "Confirm Your Account",
                    body: $"Hi {AppUser.FirstName},<br><br>Please confirm your email by clicking the link."
                );
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
            }
            var companyDto = new CompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                Email = company.Email,
                HeadOffice = company.HeadOffice,
                CreatedAt = company.CreatedAt,
                UpdatedAt = company.UpdatedAt,
                TotalInventoryValue = 0,
                TotalPurchases = 0,
                SalesTrend = 0,
                TopProductsBySales = new List<ProductDto>(),
                TotalSalesPerMonth = 0,
                LowOnStockProducts = new List<ProductDto>()
            };
            return Result<CompanyDto>.SuccessResponse(companyDto);
        }


        public Task<Result<string>> DeleteCompany(Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<CompanyDto>> GetCompanyById(Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<string>> UpdateCompany(Guid companyId, CompanyUpdateDto dto)
        {
            throw new NotImplementedException();
        }
    }
}
