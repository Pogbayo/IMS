using Hangfire.Common;
using IMS.Application.ApiResponse;
using IMS.Application.Helpers;
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
    public class SupplierService : ISupplierService
    {
        private readonly ILogger<SupplierService> _logger;
        private readonly IJobQueue _jobqueue;
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAuditService _audit;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICustomMemoryCache _cache;

        public SupplierService(
            IJobQueue jobqueue,
            UserManager<AppUser> userManager,
            IAppDbContext context,
            ILogger<SupplierService> logger,
            ICurrentUserService currentUserService,
            IAuditService audit,
            ICustomMemoryCache cache)
        {
            _context = context;
            _jobqueue = jobqueue;
            _userManager = userManager;
            _logger = logger;
            _currentUserService = currentUserService;
            _audit = audit;
            _cache = cache;
        }

        private async Task<Guid> GetCurrentUserCompanyIdAsync()
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await _userManager.Users
                .Where(u => u.Id == userId)
                .Select(u => u.CompanyId)
                .FirstOrDefaultAsync();

            if (!companyId.HasValue)
                _logger.LogWarning("User {UserId} does not belong to any company", userId);

            return companyId.Value;
        }

        public async Task<Result<string>> DeleteSupplier(Guid supplierId)
        {
            if (supplierId == Guid.Empty)
            {
                _logger.LogWarning("DeleteSupplier failed: SupplierId is empty.");
                return Result<string>.FailureResponse("Supplier ID cannot be empty.");
            }

            try
            {
                var supplier = await _context.Suppliers.FindAsync(supplierId);

                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();
                var company = await _context.Companies
                    .Where(n => n.Id == companyId)
                    .Select(n => new { n.Name, n.Email })
                    .FirstOrDefaultAsync();

                if (supplier == null)
                {
                    _logger.LogWarning("DeleteSupplier failed: Supplier with ID {SupplierId} not found.", supplierId);

                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Delete,
                        $"Failed delete attempt: Supplier {supplierId} not found.");

                    return Result<string>.FailureResponse("Supplier not found.");
                }

                supplier.MarkAsDeleted();
                await _context.SaveChangesAsync();

                _cache.RemoveByPrefix($"Suppliers_{companyId}_");

                var body = $@"
                    Hello {supplier.Name},

                    We would like to inform you that your supplier account associated with {company!.Name} has been removed from our system.

                    This action means you will no longer have access to supplier operations, product management, or communications within the company's platform.

                    If you believe this was done in error or you would like further clarification, please feel free to contact our support team at {company.Email}.

                    Thank you for your time with us, and we wish you the best in your future endeavours.

                    Best regards,
                    The {company.Name} Team
                    ";

                _jobqueue.EnqueueEmail(supplier.Email, "Notice of Removal", body);
                _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Delete,
                    $"Supplier '{supplier.Name}' (ID: {supplier.Id}) deleted by User {userId}");

                _logger.LogInformation("Supplier '{SupplierName}' (ID {SupplierId}) successfully deleted.", supplier.Name, supplier.Id);

                return Result<string>.SuccessResponse("Supplier deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while deleting Supplier {SupplierId}", supplierId);
                var companyId = await GetCurrentUserCompanyIdAsync();

                _jobqueue.EnqueueAudit(_currentUserService.GetCurrentUserId(), companyId, AuditAction.Error,
                    $"DeleteSupplier exception for Supplier {supplierId}");

                return Result<string>.FailureResponse("Error deleting supplier.");
            }
        }

        public async Task<Result<List<SupplierDto>>> GetAllSuppliers()
        {
            try
            {
                _logger.LogInformation("GetAllSuppliers started.");

                var companyId = await GetCurrentUserCompanyIdAsync();
                var userId = _currentUserService.GetCurrentUserId();

                if (companyId == Guid.Empty)
                {
                    _logger.LogWarning("GetAllSuppliers failed: CompanyId missing.");

                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, "GetAllSuppliers failed: empty CompanyId.");
                    return Result<List<SupplierDto>>.FailureResponse("Company ID is required.");
                }

                string cacheKey = $"Suppliers_{companyId}_All";

                var suppliers = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                    var list = await _context.Suppliers
                        .Where(s => s.CompanyId == companyId)
                        .Select(s => new SupplierDto
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Email = s.Email,
                            PhoneNumber = s.PhoneNumber
                        })
                        .ToListAsync();

                    if (list.Count() == 0)
                        return null; 

                    return list;
                });

                if (suppliers == null || !suppliers.Any())
                {
                    _logger.LogInformation("No suppliers found for CompanyId {CompanyId}.", companyId);

                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, "No suppliers found.");
                    return Result<List<SupplierDto>>.FailureResponse("No suppliers found.");
                }

                _logger.LogInformation("Retrieved {Count} suppliers for CompanyId {CompanyId}.", suppliers.Count, companyId);

                _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Retrieved {suppliers.Count} suppliers.");

                return Result<List<SupplierDto>>.SuccessResponse(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred in GetAllSuppliers.");
                var companyId = await GetCurrentUserCompanyIdAsync();
                _jobqueue.EnqueueAudit(_currentUserService.GetCurrentUserId(), companyId, AuditAction.Failed,
                    "GetAllSuppliers threw an exception.");

                return Result<List<SupplierDto>>.FailureResponse("Error fetching suppliers.");
            }
        }

        public async Task<Result<string>> RegisterSupplierToCompany(Guid companyId, SupplierCreateDto dto)
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("RegisterSupplierToCompany called with empty Company ID.");
                return Result<string>.FailureResponse("Company ID needed to register a Supplier");
            }

            if (dto == null)
            {
                _logger.LogWarning("RegisterSupplierToCompany called with null DTO.");
                return Result<string>.FailureResponse("Bad Request");
            }

            try
            {
                _logger.LogInformation("Starting supplier registration for company ID {CompanyId} and supplier {SupplierName}", companyId, dto.Name);

                var company = await _context.Companies.FindAsync(companyId);
                if (company == null)
                    return Result<string>.FailureResponse("Company not found");

                var supplier = new Supplier
                {
                    Name = dto.Name,
                    Email = dto.Email,
                    CompanyId = companyId,
                    Products = new List<Product>()
                };

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                _cache.RemoveByPrefix($"Suppliers_{companyId}_");

                var body = $@"
                    Hello {supplier.Name},

                    Welcome to {company.Name}! 🎉

                    Your supplier account has been successfully registered with our system. 
                    You can now access our platform to manage your products, orders, and communications with {company.Name}.

                    If you have any questions or need assistance, please feel free to reach out to our support team at {company.Email}.

                    Thank you for joining us!

                    Best regards,
                    The {company.Name} Team
                    ";

                _jobqueue.EnqueueEmail(supplier.Email!, "Company Registration!", body);
              

                var userId = _currentUserService.GetCurrentUserId();

                _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Create,
                    $"Supplier '{supplier.Name}' was created by user {userId}");

                _logger.LogInformation("Supplier registration completed successfully for {SupplierName}", dto.Name);
                return Result<string>.SuccessResponse("Supplier created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unknown error occurred while registering supplier {SupplierName} to company ID {CompanyId}", dto.Name, companyId);
                throw new Exception("An unknown error occurred while registering Supplier to the Company", ex);
            }
        }

        public async Task<Result<string>> UpdateSupplier(SupplierUpdateDto dto)
        {
            if (dto == null || dto.Id == Guid.Empty)
            {
                _logger.LogWarning("UpdateSupplier failed: Invalid DTO or missing Supplier ID.");
                return Result<string>.FailureResponse("Invalid request.");
            }

            try
            {
                var supplier = await _context.Suppliers.FindAsync(dto.Id);
                if (supplier == null)
                    return Result<string>.FailureResponse("Supplier not found.");

                supplier.Name = dto.Name ?? supplier.Name;
                supplier.Email = dto.Email ?? supplier.Email;
                supplier.PhoneNumber = dto.PhoneNumber ?? supplier.PhoneNumber;
                if (dto.IsActive.HasValue)
                    supplier.IsActive = dto.IsActive.Value;

                supplier.MarkAsUpdated();
                await _context.SaveChangesAsync();

                _cache.RemoveByPrefix($"Suppliers_{supplier.CompanyId}_");

                var userId = _currentUserService.GetCurrentUserId();
                _jobqueue.EnqueueAudit(userId, supplier.CompanyId, AuditAction.Update,
                    $"Supplier {supplier.Name} (ID: {supplier.Id}) was updated by {userId}");

                return Result<string>.SuccessResponse("Supplier updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supplier with ID {SupplierId}", dto.Id);
                _jobqueue.EnqueueAudit(_currentUserService.GetCurrentUserId(), Guid.Empty, AuditAction.Error,
                    $"UpdateSupplier encountered an exception for Supplier {dto.Id}");
                return Result<string>.FailureResponse("An error occurred while updating the supplier.");
            }
        }

        public async Task<Result<SupplierDto>> GetSupplierByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("GetSupplierByName called with empty name.");
                return Result<SupplierDto>.FailureResponse("Supplier name is required.");
            }

            try
            {
                var companyId = await GetCurrentUserCompanyIdAsync();
                var userId = _currentUserService.GetCurrentUserId();

                if (companyId == Guid.Empty)
                {
                    _logger.LogWarning("GetSupplierByName failed: CompanyId missing.");
                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read,
                        "GetSupplierByName failed: empty CompanyId.");
                    return Result<SupplierDto>.FailureResponse("Company ID is required.");
                }

                string cacheKey = $"Suppliers_{companyId}_{name.ToLower()}";

                var supplier = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                    return await _context.Suppliers
                        .Where(s => s.CompanyId == companyId && s.Name.ToLower() == name.ToLower())
                        .Select(s => new SupplierDto
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Email = s.Email,
                            PhoneNumber = s.PhoneNumber
                        })
                        .FirstOrDefaultAsync();
                });

                if (supplier == null)
                {
                    _logger.LogInformation("No supplier found with name '{Name}' in CompanyId {CompanyId}.", name, companyId);
                    _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read,
                        $"No supplier found with name '{name}'.");
                    return Result<SupplierDto>.FailureResponse("Supplier not found.");
                }

                _logger.LogInformation("Supplier '{Name}' retrieved successfully for CompanyId {CompanyId}.", name, companyId);
                _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read,
                    $"Supplier '{name}' retrieved successfully.");

                return Result<SupplierDto>.SuccessResponse(supplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supplier by name '{Name}'.", name);

                var companyId = await GetCurrentUserCompanyIdAsync();

                _jobqueue.EnqueueAudit(userId: _currentUserService.GetCurrentUserId(),
                    companyId,
                    AuditAction.Error,
                    $"GetSupplierByName error for supplier '{name}'");

                return Result<SupplierDto>.FailureResponse("Error fetching supplier.");
            }
        }

    }
}
