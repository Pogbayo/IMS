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
using System.Text.Json;

namespace IMS.Application.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ILogger<CompanyService> _logger;
        private readonly IAppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICompanyCalculations _companyCalculations;
        private readonly IWarehouseService _warehouseService;
        private readonly ICustomMemoryCache _cache;
        private readonly IJobQueue _jobqueue;
        private readonly IMailerService _mailer;
        private readonly IPhoneValidator _phonevalidator;

        public CompanyService(
                        IPhoneValidator phonevalidator,
                        IJobQueue jobqueue,
                        IMailerService mailer,
                        ILogger<CompanyService> logger,
                        IWarehouseService warehouseService,
                        IAppDbContext context,
                        UserManager<AppUser> userManager,
                        RoleManager<IdentityRole<Guid>> roleManager,
                        IAuditService audit,
                        ICurrentUserService currentUserService,
                        ICompanyCalculations companyCalculations,
                        ICustomMemoryCache cache
        )
        {
            _jobqueue = jobqueue;
            _phonevalidator = phonevalidator;
            _mailer = mailer;
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _warehouseService = warehouseService;
            _audit = audit;
            _companyCalculations = companyCalculations;
            _currentUserService = currentUserService;
            _cache = cache;
        }

        public async Task<Result<CreatedCompanyDto>> RegisterCompanyAndAdmin(CompanyCreateDto dto)
        {
            if (dto == null)
                return Result<CreatedCompanyDto>.FailureResponse("Invalid request", "Company details are required");

            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();
            try
            {
                var company = new Company
                {
                    Name = dto.CompanyName!,
                    CompanyEmail = dto.CompanyEmail!,
                    HeadOffice = dto.HeadOffice!
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                await _phonevalidator.Validate(dto.AdminPhoneNumber!);

                var Admin = new AppUser
                {
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    UserName = dto.Email,
                    CompanyId = company.Id,
                    IsCompanyAdmin = true,
                    PhoneNumber = dto.AdminPhoneNumber!
                };

                var identityResult = await _userManager.CreateAsync(Admin, dto.Password);

                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = string.Join("; ", identityResult.Errors.Select(e => e.Description));

                    _jobqueue.EnqueueAudit(Admin.Id, company.Id, AuditAction.Create, $"Failed company registration attempt for {dto.CompanyEmail}: {errors}");
                    _jobqueue.EnqueueCloudWatchAudit($"{Admin.Id}Failed company registration attempt for {dto.CompanyEmail}: {errors}");

                    return Result<CreatedCompanyDto>.FailureResponse(
                        $"User registration failed: {errors}"
                    );
                }

                if (!await _roleManager.RoleExistsAsync("Admin"))
                {
                    var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));
                    if (!roleResult.Succeeded)
                        throw new InvalidOperationException("Admin role creation failed");
                }

                var roleAssignResult = await _userManager.AddToRoleAsync(Admin, "Admin");
                if (!roleAssignResult.Succeeded)
                    throw new InvalidOperationException("Failed to assign Admin role");

                company.CreatedById = Admin.Id;
                company.Users.Add(Admin);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                try
                {
                    _jobqueue.EnqueueAudit(Admin.Id, company.Id, AuditAction.Create, $"Company '{company.Name}' registered with admin '{Admin.Email}'.");
                    _jobqueue.EnqueueCloudWatchAudit($"Company '{company.Name}' registered with admin '{Admin.Email}'.");

                    //_jobqueue.EnqueueEmail(Admin.Email, "Welcome!", $"Hi {Admin.FirstName}, InvManager welcomes you on board.");
                    _jobqueue.EnqueueAWS_Ses(new List<string> { Admin.Email},"Welcome!", $"Hi {Admin.FirstName}, welcome to InvManager! We’re glad to have you with us and look forward to supporting you every step of the way.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Post-registration tasks failed");
                    _jobqueue.EnqueueCloudWatchAudit($"Post-registration tasks failed for company '{company.Name}': {ex.Message}");
                }

                return Result<CreatedCompanyDto>.SuccessResponse(new CreatedCompanyDto
                {
                    AdminId = Admin.Id,
                    CompanyId = company.Id,
                    Name = company.Name!,
                    FirstName = company.CreatedBy.FirstName,
                    CreatedAt = company.CreatedAt,
                    CompanyEmail = company.CompanyEmail,
                    HeadOffice = company.HeadOffice,
                    AdminEmail = Admin.Email,
                    UpdatedAt = company.UpdatedAt,
                    TotalInventoryValue = 0,
                    TotalPurchases = 0,
                    SalesTrend = 0,
                    TopProductsBySales = new(),
                    TotalSalesPerMonth = 0,
                    LowOnStockProducts = new()
                }, "Company registered Successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Company registration failed for {Email}", dto.CompanyEmail);

                _jobqueue.EnqueueCloudWatchAudit($"Company registration failed for {dto.CompanyEmail}: {ex.InnerException?.Message ?? ex.Message}");

                return Result<CreatedCompanyDto>.FailureResponse(
                    "Transaction not complete",
                    ex.InnerException?.Message ?? ex.Message
                );
            }
        }

        public async Task<Result<string>> DeleteCompany(Guid companyId)
        {
            var currentUserId = _currentUserService.GetCurrentUserId();
            if (companyId == Guid.Empty)
            {
                await _audit.LogAsync(currentUserId, companyId, AuditAction.Failed, "Attempted to delete company but Id was empty");
                _jobqueue.EnqueueCloudWatchAudit($"User {currentUserId} attempted to delete company but Id was empty");
                return Result<string>.FailureResponse("Id cannot be null");
            }

            try
            {
                var company = await _context.Companies.FindAsync(companyId);
                if (company == null)
                {
                    _jobqueue.EnqueueAudit(currentUserId, companyId, AuditAction.Failed, "Attempted to delete company but company not found");
                    _jobqueue.EnqueueCloudWatchAudit($"User {currentUserId} attempted to delete company {companyId} but it was not found");
                    return Result<string>.FailureResponse("Company not found");
                }

                foreach (var user in company.Users)
                {
                    user.Tokenversion++;
                    user.MarkAsDeleted();
                    _jobqueue.EnqueueAudit(user.Id, company.Id, AuditAction.Invalidate, $"{user.FirstName} has been invalidated");
                    _jobqueue.EnqueueCloudWatchAudit($"User {user.Id} invalidated due to company deletion {companyId}");
                    //_jobqueue.EnqueueEmail(user.Email!, "IMPORTANT NOTICE!!!!", $"Dear Customer, we are sorry to inform you that your account has been invalidated because company {company.Name} was deleted.");
                    _jobqueue.EnqueueAWS_Ses(new List<string> { user.Email! }, "IMPORTANT NOTICE!!!!", $"Dear Customer, we are sorry to inform you that your account has been invalidated because company {company.Name} was deleted.");
                }

                company.MarkAsDeleted();
                await _context.SaveChangesAsync();

                _jobqueue.EnqueueAudit(currentUserId, companyId, AuditAction.Delete, "Company deleted successfully");
                _jobqueue.EnqueueCloudWatchAudit($"Company {companyId} deleted successfully by user {currentUserId}");

                _cache.Remove($"Company_{companyId}");
                for (int page = 1; page <= 100; page++) 
                {
                    _cache.Remove($"Companies_Page{page}_Size{10}");
                }

                return Result<string>.SuccessResponse("Company deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting company");
                 _jobqueue.EnqueueAudit(currentUserId, companyId, AuditAction.Failed, "Unexpected error deleting company");
                _jobqueue.EnqueueCloudWatchAudit($"Unexpected error deleting company {companyId} by user {currentUserId}: {ex.Message}");
                return Result<string>.FailureResponse("An unexpected error occurred while deleting the company");
            }
        }

        public async Task<Result<CompanyDto>> GetCompanyById(Guid companyId)
        {
            var cacheKey = $"Company_{companyId}";

            var result = await _cache.GetOrCreateAsync<Result<CompanyDto>>(cacheKey, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                var userId = _currentUserService.GetCurrentUserId();
                var today = DateTime.UtcNow.Date;

                if (companyId == Guid.Empty)
                {
                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Failed, "Attempted to fetch company but Id is empty");
                    _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch company but Id was empty");
                    return Result<CompanyDto>.FailureResponse("Id cannot be null");
                }

                var todayStat = await _context.CompanyDailyStats.FirstOrDefaultAsync(st => st.CompanyId == companyId && st.StatDate == today);

                var stockTransactions = _context.StockTransactions
                    .Include(st => st.ProductWarehouse)
                    .ThenInclude(pw => pw!.Product)
                    .Where(st => st.CompanyId == companyId);

                var company = await _context.Companies
                    .Include(c => c.CreatedBy)
                    .FirstOrDefaultAsync(c => c.Id == companyId);

                if (company == null)
                {
                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Failed, "Company not found");
                    _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch company {companyId} but it was not found");
                    return Result<CompanyDto>.FailureResponse("Company not found");
                }

                if (company.CreatedBy == null)
                {
                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Failed, "Company has no creator assigned");
                    _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch company {companyId} but creator was null");
                    return Result<CompanyDto>.FailureResponse("Company creator not found");
                }

                var ProductsCount = company.Products.Count();

                var companyDto = new CompanyDto
                {
                    Id = company.Id,
                    Name = company.Name,
                    CompanyEmail = company.CompanyEmail,
                    AdminEmail = company.CreatedBy.Email ?? "Unknown",
                    HeadOffice = company.HeadOffice,
                    CreatedAt = company.CreatedAt,
                    LastUpdated = company.UpdatedAt,
                    TotalPurchases = await _companyCalculations.CalculateTotalPurchases(stockTransactions),
                    SalesTrend = await _companyCalculations.CalculateTotalSalesTrend(stockTransactions),
                    TotalSalesPerMonth = await _companyCalculations.TotalSalesPerMonth(stockTransactions),
                    TotalNumberOfProducts = ProductsCount,
                    TotalInventoryValue = todayStat != null ? todayStat.TotalInventoryValue : 0m,
                    TopProductsBySales = todayStat != null
                        ? JsonSerializer.Deserialize<List<TopProductDto>>(todayStat.TopProductsBySalesJson) ?? new()
                        : new(),
                    LowOnStockProducts = todayStat != null
                        ? JsonSerializer.Deserialize<List<LowOnStockProduct>>(todayStat.LowOnStockProductsJson) ?? new()
                        : new()
                };

                _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Viewed dashboard for company '{company.Name}'");
                _jobqueue.EnqueueCloudWatchAudit($"User {userId} viewed dashboard for company '{company.Name}'");

                return Result<CompanyDto>.SuccessResponse(companyDto);
            });

            return result ?? Result<CompanyDto>.FailureResponse("Failed to retrieve company data from cache");
        }

        public async Task<Result<string>> UpdateCompany(Guid companyId, CompanyUpdateDto dto)
        {
            var currentUserId = _currentUserService.GetCurrentUserId();

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null)
            {
                _jobqueue.EnqueueAudit(currentUserId, companyId, AuditAction.Failed, "Attempted to update company but not found");
                _jobqueue.EnqueueCloudWatchAudit($"User {currentUserId} attempted to update company {companyId} but it was not found");
                return Result<string>.FailureResponse("Company not found");
            }

            company.Name = dto?.Name ?? company.Name;
            company.AdminEmail = dto?.AdminEmail ?? company.AdminEmail;
            company.HeadOffice = dto?.HeadOffice ?? company.HeadOffice;
            company.CompanyEmail = dto?.CompanyEmail ?? company.CompanyEmail;
            company.MarkAsUpdated();

            await _context.UpdateChangesAsync(company);

            _jobqueue.EnqueueAudit(currentUserId, companyId, AuditAction.Update, $"Company: {companyId} updated by {currentUserId}");
            _jobqueue.EnqueueCloudWatchAudit($"Company {companyId} updated by user {currentUserId}");

            _cache.Remove($"Company_{companyId}");

            return Result<string>.SuccessResponse("Company updated Successfully");
        }

        public async Task<Result<List<AllCompaniesDto>>> GetAllCompanies(int pageSize, int pageNumber)
        {
            var cacheKey = $"Companies_Page{pageNumber}_Size{pageSize}";

            var result = await _cache.GetOrCreateAsync<Result<List<AllCompaniesDto>>>(cacheKey, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                if (pageNumber < 1 || pageSize < 1)
                {
                    _jobqueue.EnqueueCloudWatchAudit($"Invalid pagination parameters: page {pageNumber}, size {pageSize}");
                    return Result<List<AllCompaniesDto>>.FailureResponse("PageNumber and PageSize cannot be less than 1");
                }

                var companies = await _context.Companies
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new AllCompaniesDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        CompanyEmail = c.CompanyEmail,
                        HeadOffice = c.HeadOffice,
                        AdminEmail = c.AdminEmail!
                    })
                    .ToListAsync();

                if (!companies.Any())
                {
                    _jobqueue.EnqueueCloudWatchAudit($"User {_currentUserService.GetCurrentUserId()} fetched companies page {pageNumber} but no companies found");
                    return Result<List<AllCompaniesDto>>.FailureResponse("No companies found");
                }

                _jobqueue.EnqueueCloudWatchAudit($"User {_currentUserService.GetCurrentUserId()} fetched companies page {pageNumber} with {companies.Count} results");

                return Result<List<AllCompaniesDto>>.SuccessResponse(companies);
            }) ?? Result<List<AllCompaniesDto>>.FailureResponse("Failed to fetch companies from cache");

            return result;
        }
    }
}
