using Hangfire;
using IMS.Application.ApiResponse;
using IMS.Application.DTO.Company;
using IMS.Application.DTO.Product;
using System.Text.Json;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Mailer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        private readonly ICurrentUserService _currentUserService;
        private readonly ICompanyCalculations _companyCalculations;
        private readonly IWarehouseService _warehouseService;
        public CompanyService(
            ILogger<CompanyService> logger,
            IWarehouseService warehouseService,
            IAppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService audit,
            ICurrentUserService currentUserService,
            ICompanyCalculations companyCalculations
            )
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _warehouseService = warehouseService;
            _audit = audit;
            _companyCalculations = companyCalculations;
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

            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();

            try
            {
                var createdWarehouses = new List<Warehouse>();

                foreach (var item in dto.Warehouses)
                {
                    var result = await _warehouseService.CreateWarehouse(item);
                    if (!result.Success)
                        continue;

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

                //await _audit.LogAsync(
                //    userId: AppUser.Id,
                //    companyId: company.Id,
                //    action: AuditAction.Create,
                //    description: $"Admin user '{AppUser.FirstName} {AppUser.LastName}' created for company '{company.Name}'."
                //);

                var IdentityResult = await _userManager.CreateAsync(AppUser, dto.Password);

                if (!IdentityResult.Succeeded)
                {
                    var errorMessages = string.Join("; ", IdentityResult.Errors.Select(e => e.Description));
                    return Result<CreatedCompanyDto>.FailureResponse($"Error registering user: {errorMessages}");
                }

                await _audit.LogAsync(
                   userId: AppUser.Id,
                   companyId: company.Id,
                   action: AuditAction.Create,
                   description: $"Admin user '{AppUser.FirstName} {AppUser.LastName}' registered with company '{company.Name}'."
               );

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

                    await _audit.LogAsync(
                       userId: AppUser.Id,
                       companyId: company.Id,
                       action: AuditAction.Update,
                       description: $"An unknwown error occurred while assigning role to User: {AppUser.Id}'."
                     );
                }

                _logger.LogInformation("Transaction complete");
                return Result<CreatedCompanyDto>.SuccessResponse(createdCompanyDto);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                _logger.LogInformation("Transaction not complete");
                return Result<CreatedCompanyDto>.FailureResponse("Trnasaction not complete");
            }
        }

        public async Task<Result<string>> DeleteCompany(Guid companyId)
        {
            var currentUserId = _currentUserService.GetCurrentUserId();
            _logger.LogInformation($"{currentUserId} is Attempting to delete {companyId}");
            if (companyId == Guid.Empty)
            {
                _logger.LogInformation($"Please, provide an Id");
                await _audit.LogAsync(
                    currentUserId,
                    companyId,
                    AuditAction.Failed,
                    "Attempted to delete company but Id was empty"
                );
                return Result<string>.FailureResponse("Id can not be null;");
            }

            try
            {
                var company = await _context.Companies.FindAsync(companyId);
                if (company == null)
                {
                    _logger.LogInformation($"Company not found");
                    await _audit.LogAsync(
                        currentUserId,
                        companyId,
                        AuditAction.Failed,
                        "Attempted to delete company but company not found"
                    );
                    return Result<string>.FailureResponse("Company with provided Id not found");
                }

                company.MarkAsDeleted();
                await _context.SaveChangesAsync();

                await _audit.LogAsync(
                    currentUserId,
                    companyId,
                    AuditAction.Delete,
                    "Company deleted successfully"
                );
                return Result<string>.SuccessResponse("Company deleted successfully");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting company");

                await _audit.LogAsync(
                    currentUserId,
                    companyId,
                    AuditAction.Failed,
                    "Database error deleting company"
                );
                return Result<string>.FailureResponse("A database error occurred while deleting the company");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting company");
                await _audit.LogAsync(
                    currentUserId,
                    companyId,
                    AuditAction.Failed,
                    "Unexpected error deleting company"
                );
                return Result<string>.FailureResponse("An unexpected error occurred while deleting the company");
            }
        }

        public async Task<Result<CompanyDto>> GetCompanyById(Guid companyId)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var today = DateTime.UtcNow.Date;

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

            var todayStat = await _context.CompanyDailyStats.FirstOrDefaultAsync(st => st.CompanyId == companyId && st.StatDate == today);

            var productWarehouses = _context.ProductWarehouses
                .Include(pw => pw.Product)
                .Where(pw => pw.Product!.CompanyId == companyId);

            var warehouses = _context.Warehouses
                .Include(c => c.ProductWarehouses)
                    .ThenInclude(pw => pw.Product)
                .Where(c => c.CompanyId == companyId);

            var stockTransactions = _context.StockTransactions
                .Include(st => st.ProductWarehouse)
                    .ThenInclude(pw => pw!.Product)
                .Where(st => st.CompanyId == companyId);

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null)
            {
                _logger.LogWarning("Company not found");

                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Failed,
                    "Attempted to fetch company by Id but company not found"
                );

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

            await _audit.LogAsync(
                userId,
                companyId,
                AuditAction.Read,
                $"User: {userId} viewed company dashboard for: {company.Name}"
            );
            return Result<CompanyDto>.SuccessResponse(companyDto);
        }

        public async Task<Result<string>> UpdateCompany(Guid companyId, CompanyUpdateDto dto)
        {
            var currentUserId = _currentUserService.GetCurrentUserId();

            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
            {
                _logger.LogWarning("Company not found");

                await _audit.LogAsync(
                    currentUserId,
                    companyId,
                    AuditAction.Failed,
                    "Attempted to fetch company by Id to be modified but company not found"
                );

                return Result<string>.FailureResponse("Company not found");
            }

            if (dto is null)
            {
                _logger.LogWarning("Bad request");
                return Result<string>.FailureResponse("Invalid request...Please, provide accurate details");
            }

            company.Name = dto?.Name ?? string.Empty;
            company.Email = dto?.Email ?? string.Empty;
            company.HeadOffice = dto?.HeadOffice ?? string.Empty;
            company.MarkAsUpdated();

            await _context.UpdateChangesAsync(company);

            await _audit.LogAsync(
                   currentUserId,
                   companyId,
                   AuditAction.Failed,
                   $"Company: {companyId} updated by {currentUserId}"
             );

            return Result<string>.SuccessResponse("Company updated Successfully");
        }

        public async Task<Result<List<CompanyDto>>> GetAllCompanies(int pageSize, int pageNumber)
        {
            if (pageNumber < 0 && pageSize < 0)
            {
                _logger.LogWarning("PageNumber and pageSie cannot be less than 0");
                return Result<List<CompanyDto>>.FailureResponse("PageNumber and PageSize can not be less than 0");
            }

            try
            {
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
                {
                    _logger.LogInformation("No companies found for page {PageNumber} with page size {PageSize}", pageNumber, pageSize);
                    return Result<List<CompanyDto>>.FailureResponse("No companies found");
                }

                return Result<List<CompanyDto>>.SuccessResponse(companies);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error fetching companies for page {PageNumber}", pageNumber);
                return Result<List<CompanyDto>>.FailureResponse("An error occurred while fetching companies");
            }
        }
    }
}
