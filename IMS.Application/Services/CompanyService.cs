using Hangfire;
using IMS.Application.ApiResponse;
using IMS.Application.DTO.Company;
using IMS.Application.DTO.Product;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Mailer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static System.Net.WebRequestMethods;

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
        public async Task<Result<CreatedCompanyDto>> RegisterCompanyAndAdmin(CompanyCreateDto dto)
        {
            var currentuser = _currentUserService.GetCurrentUserId();

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
            await _audit.LogAsync(
                userId: currentuser, 
                companyId: company.Id,
                action: AuditAction.Create,
                description: $"Company '{company.Name}' created with email '{company.Email}'."
            );

            var AppUser = new AppUser
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Company = company,
                CreatedCompany = company,
                IsCompanyAdmin = true
            };

            await _audit.LogAsync(
                userId: AppUser.Id,
                companyId: company.Id,
                action: AuditAction.Create,
                description: $"Admin user '{AppUser.FirstName} {AppUser.LastName}' created for company '{company.Name}'."
            );

            var IdentityResult = await _userManager.CreateAsync(AppUser, dto.Password);

            if (!IdentityResult.Succeeded)
            {
                var errorMessages = string.Join("; ", IdentityResult.Errors.Select(e => e.Description));
                return Result<CreatedCompanyDto>.FailureResponse($"Error registering user: {errorMessages}");
            }

            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            await _userManager.AddToRoleAsync(AppUser, "Admin");

            await _audit.LogAsync(
                userId: AppUser.Id,
                companyId: company.Id,
                action: AuditAction.Update,
                description: $"Admin role assigned to user '{AppUser.Email}'."
            );
            company.Users.Add(AppUser);
            company.CreatedById = AppUser.Id;

            try
            {
                string confirmationUrl = "https://superaqual-nelle-aerogenically.ngrok-free.dev/suppliers";
                string body = $"Hi {AppUser.FirstName},<br><br>Please confirm your email by clicking the link {confirmationUrl}.";

                BackgroundJob.Enqueue<IMailerService>(mailer =>
                    mailer.SendEmailAsync(AppUser.Email!, "Confirm Your Account", body)
                );

                await _audit.LogAsync(
                    userId: AppUser.Id,
                    companyId: company.Id,
                    action: AuditAction.Create,
                    description: $"Confirmation email sent to '{AppUser.Email}'."
                );
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
            }

            var createdCompanyDto = new CreatedCompanyDto
            {
                AdminId = AppUser.Id,
                CompanyId = company.Id,
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
            return Result<CreatedCompanyDto>.SuccessResponse(createdCompanyDto);
        }


        public Task<Result<string>> DeleteCompany(Guid companyId)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<CompanyDto>> GetCompanyById(Guid companyId)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Please, provide an Id to complete request");
                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Failed,
                    "Attempted to fetch company but provided an empty Id"
                );

                return Result<CompanyDto>.FailureResponse("Id cannot be null");
            }

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null)
            {
                _logger.LogWarning("Company not found");

                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Failed,
                    "Attempted to fetch company but company not found"
                );

                return Result<CompanyDto>.FailureResponse("Company not found");
            }

            var productWarehouses = _context.ProductWarehouses
                .Include(pw => pw.Product)
                .Where(pw => pw.Product!.CompanyId == companyId)
                .AsQueryable();

            var warehouses = _context.Warehouses
                .Include(c => c.ProductWarehouses)
                    .ThenInclude(pw => pw.Product)
                .Where(c => c.CompanyId == companyId);

            var stockTransactions = _context.StockTransactions
                .Include(st => st.ProductWarehouse)
                    .ThenInclude(pw => pw!.Product)
                .Where(st => st.CompanyId == companyId);

            
            var companyDto = new CompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                Email = company.Email,
                HeadOffice = company.HeadOffice,
                CreatedAt = company.CreatedAt,
                UpdatedAt = company.UpdatedAt,
                TotalInventoryValue = CompanyCalculations.CalculateTotalInventoryValue(warehouses),
                TotalPurchases = CompanyCalculations.CalculateTotalPurchases(stockTransactions),
                SalesTrend = CompanyCalculations.CalculateTotalSalesTrend(stockTransactions),
                TopProductsBySales = CompanyCalculations.TopProductBySales(stockTransactions).ToList(),
                TotalSalesPerMonth = CompanyCalculations.TotalSalesPerMonth(stockTransactions),
                LowOnStockProducts = CompanyCalculations.GetLowOnStockProducts(productWarehouses)
            };

            await _audit.LogAsync(
                userId,
                companyId,
                AuditAction.Read,
                $"User viewed company dashboard for: {company.Name}"
            );

            return Result<CompanyDto>.SuccessResponse(companyDto);
        }

        public Task<Result<string>> UpdateCompany(Guid companyId, CompanyUpdateDto dto)
        {
            throw new NotImplementedException();
        }
    }
}
