using IMS.Application.ApiResponse;
using IMS.Application.DTO.Company;
using IMS.Application.DTO.Product;
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
        public CompanyService(
            IJobQueue jobqueue,
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
            var currentuser = _currentUserService.GetCurrentUserId();

            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "Incomplete details -- Company details are required.");

            if (string.IsNullOrWhiteSpace(dto.CompanyName))
                throw new ArgumentNullException(nameof(dto.CompanyName));

            if (string.IsNullOrWhiteSpace(dto.CompanyEmail))
                throw new ArgumentNullException(nameof(dto.CompanyEmail));

            if (string.IsNullOrWhiteSpace(dto.HeadOffice))
                throw new ArgumentNullException(nameof(dto.HeadOffice));

            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();

            try
            {
                var createdWarehouses = new List<Warehouse>();

                foreach (var item in dto.Warehouses)
                {
                    var result = await _warehouseService.CreateWarehouse(item);
                    if (!result.Success) continue;

                    var warehouseEntity = await _context.Warehouses.FindAsync(result.Data);
                    if (warehouseEntity != null)
                        createdWarehouses.Add(warehouseEntity);
                }

                var company = new Company
                {
                    Name = dto.CompanyName,
                    Email = dto.CompanyEmail,
                    HeadOffice = dto.HeadOffice,
                    Warehouses = createdWarehouses,
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                await _audit.LogAsync(currentuser, company.Id, AuditAction.Create, $"Company '{company.Name}' created with email '{company.Email}'.");

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
                    return Result<CreatedCompanyDto>.FailureResponse($"Error registering user: {errorMessages}");
                }

                await _audit.LogAsync(AppUser.Id, company.Id, AuditAction.Create, $"Admin user '{AppUser.FirstName} {AppUser.LastName}' registered with company '{company.Name}'.");

                if (!await _roleManager.RoleExistsAsync("Admin"))
                    await _roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));

                await _userManager.AddToRoleAsync(AppUser, "Admin");

                await _audit.LogAsync(AppUser.Id, company.Id, AuditAction.Update, $"Admin role assigned to user '{AppUser.Email}'.");

                company.Users.Add(AppUser);
                company.CreatedById = AppUser.Id;
                await _context.SaveChangesAsync();

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
                    TopProductsBySales = new List<ProductsDto>(),
                    TotalSalesPerMonth = 0,
                    LowOnStockProducts = new List<ProductsDto>()
                };

                await transaction.CommitAsync();

                // Confirmation email
                try
                {
                    string confirmationUrl = "https://superaqual-nelle-aerogenically.ngrok-free.dev/ims/dashboard";
                    string body = $"Hi {AppUser.FirstName},<br><br>Please confirm your email by clicking the link {confirmationUrl}.";

                    _jobqueue.Enqueue<IMailerService>(mailer =>
                        mailer.SendEmailAsync(AppUser.Email!, "Confirm Your Account", body)
                    );

                    //BackgroundJob.Enqueue<IMailerService>(mailer =>
                    //    mailer.SendEmailAsync(AppUser.Email!, "Confirm Your Account", body)
                    //);

                    await _audit.LogAsync(AppUser.Id, company.Id, AuditAction.Create, $"Confirmation email sent to '{AppUser.Email}'.");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.Message);
                    await _audit.LogAsync(AppUser.Id, company.Id, AuditAction.Update, $"Unknown error while sending confirmation email for User: {AppUser.Id}");
                }

                return Result<CreatedCompanyDto>.SuccessResponse(createdCompanyDto);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _logger.LogInformation("Transaction not complete");
                return Result<CreatedCompanyDto>.FailureResponse("Transaction not complete");
            }
        }

        public async Task<Result<string>> DeleteCompany(Guid companyId)
        {
            var currentUserId = _currentUserService.GetCurrentUserId();
            if (companyId == Guid.Empty)
            {
                await _audit.LogAsync(currentUserId, companyId, AuditAction.Failed, "Attempted to delete company but Id was empty");
                return Result<string>.FailureResponse("Id cannot be null");
            }

            try
            {
                var company = await _context.Companies.FindAsync(companyId);
                if (company == null)
                {
                    await _audit.LogAsync(currentUserId, companyId, AuditAction.Failed, "Attempted to delete company but company not found");
                    return Result<string>.FailureResponse("Company not found");
                }

                company.MarkAsDeleted();
                await _context.SaveChangesAsync();

                await _audit.LogAsync(currentUserId, companyId, AuditAction.Delete, "Company deleted successfully");

                // Invalidate cache for this company
                _cache.Remove($"Company_{companyId}");

                return Result<string>.SuccessResponse("Company deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting company");
                await _audit.LogAsync(currentUserId, companyId, AuditAction.Failed, "Unexpected error deleting company");
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
                    await _audit.LogAsync(userId, companyId, AuditAction.Failed, "Attempted to fetch company but Id is empty");
                    return Result<CompanyDto>.FailureResponse("Id cannot be null");
                }

                var todayStat = await _context.CompanyDailyStats.FirstOrDefaultAsync(st => st.CompanyId == companyId && st.StatDate == today);

                var stockTransactions = _context.StockTransactions
                    .Include(st => st.ProductWarehouse)
                    .ThenInclude(pw => pw!.Product)
                    .Where(st => st.CompanyId == companyId);

                var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
                if (company == null)
                {
                    await _audit.LogAsync(userId, companyId, AuditAction.Failed, "Company not found");
                    return Result<CompanyDto>.FailureResponse("Company not found");
                }

                var ProductsCount = company.Products.Count();

                var companyDto = new CompanyDto
                {
                    Id = company.Id,
                    Name = company.Name,
                    Email = company.Email,
                    HeadOffice = company.HeadOffice,
                    CreatedAt = company.CreatedAt,
                    UpdatedAt = company.UpdatedAt,
                    TotalPurchases = await _companyCalculations.CalculateTotalPurchases(stockTransactions),
                    SalesTrend = await _companyCalculations.CalculateTotalSalesTrend(stockTransactions),
                    TotalSalesPerMonth = await _companyCalculations.TotalSalesPerMonth(stockTransactions),
                    TotalNumberOfProducts = ProductsCount,
                    TotalInventoryValue = todayStat != null ? todayStat.TotalInventoryValue : 0m,

                    TopProductsBySales = todayStat != null
                        ? JsonSerializer.Deserialize<List<TopProductDto>>(todayStat.TopProductsBySalesJson) ?? new List<TopProductDto>()
                        : new List<TopProductDto>(),

                    LowOnStockProducts = todayStat != null
                        ? JsonSerializer.Deserialize<List<LowOnStockProduct>>(todayStat.LowOnStockProductsJson) ?? new List<LowOnStockProduct>()
                        : new List<LowOnStockProduct>()
                };

                await _audit.LogAsync(userId, companyId, AuditAction.Read, $"User: {userId} viewed company dashboard for: {company.Name}");
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
                await _audit.LogAsync(currentUserId, companyId, AuditAction.Failed, "Attempted to update company but not found");
                return Result<string>.FailureResponse("Company not found");
            }

            company.Name = dto?.Name ?? string.Empty;
            company.Email = dto?.Email ?? string.Empty;
            company.HeadOffice = dto?.HeadOffice ?? string.Empty;
            company.MarkAsUpdated();

            await _context.UpdateChangesAsync(company);

            await _audit.LogAsync(currentUserId, companyId, AuditAction.Update, $"Company: {companyId} updated by {currentUserId}");

            // Invalidate cache for this company
            _cache.Remove($"Company_{companyId}");

            return Result<string>.SuccessResponse("Company updated Successfully");
        }

        public async Task<Result<List<CompanyDto>>> GetAllCompanies(int pageSize, int pageNumber)
        {
            var cacheKey = $"Companies_Page{pageNumber}_Size{pageSize}";

            var result = await _cache.GetOrCreateAsync<Result<List<CompanyDto>>>(cacheKey, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                if (pageNumber < 1 || pageSize < 1)
                    return Result<List<CompanyDto>>.FailureResponse("PageNumber and PageSize cannot be less than 1");

                var companies = await _context.Companies
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new CompanyDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Email = c.Email,
                        HeadOffice = c.HeadOffice
                    })
                    .ToListAsync();

                if (!companies.Any())
                    return Result<List<CompanyDto>>.FailureResponse("No companies found");

                return Result<List<CompanyDto>>.SuccessResponse(companies);
            }) ?? Result<List<CompanyDto>>.FailureResponse("Failed to fetch companies from cache");

            return result;
        }
    }
}

