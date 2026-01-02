using IMS.Application.ApiResponse;
using IMS.Application.DTO;
using IMS.Application.DTO.Warehouse;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace IMS.Application.Services
{
    public class WarehouseService : IWarehouseService
    {
        private readonly ILogger<CompanyService> _logger;
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPhoneValidator _phoneValidator;
        private readonly UserManager<AppUser> _userManager;
        private readonly IJobQueue _jobqueue;
             
        private readonly ICustomMemoryCache _cache;

        public WarehouseService(UserManager<AppUser> usermanager,
                                IJobQueue jobqueue,
                                ILogger<CompanyService> logger,
                                IAppDbContext context,
                                IAuditService audit,
                                ICurrentUserService currentUserService,
                                IPhoneValidator phonevalidator,
                                ICustomMemoryCache cache)
        {
            _logger = logger;
            _jobqueue = jobqueue;
            _context = context;
            _audit = audit;
            _currentUserService = currentUserService;
            _phoneValidator = phonevalidator;
            _userManager = usermanager;
            _cache = cache;
        }

        private async Task<Guid?> GetCurrentUserCompanyIdAsync()
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await _userManager.Users
                .Where(u => u.Id == userId)
                .Select(u => u.CompanyId)
                .FirstOrDefaultAsync();

            if (!companyId.HasValue)
                _logger.LogWarning("User {UserId} does not belong to any company", userId);

            return companyId;
        }

        public async Task<Result<Guid>> CreateWarehouse(CreateWarehouseDto dto)
        {
            if (dto == null)
                return Result<Guid>.FailureResponse("Bad Request: DTO cannot be null");

            try { await _phoneValidator.Validate(dto.PhoneNumber); }
            catch (ValidationException) { return Result<Guid>.FailureResponse("Invalid phone number"); }

            var warehouse = new Warehouse
            {
                Name = dto.Name,
                Location = dto.Location,
                CompanyId = dto.CompanyId,
                PhoneNumber = dto.PhoneNumber,
                ProductWarehouses = new List<ProductWarehouse>()
            };

            await _context.Warehouses.AddAsync(warehouse);

            try
            {
                int result = await _context.SaveChangesAsync();
                if (result < 1)
                    return Result<Guid>.FailureResponse("Warehouse could not be saved");
            }
            catch { return Result<Guid>.FailureResponse("Error saving warehouse"); }

            try
            {
                var userId = _currentUserService.GetCurrentUserId();

                _jobqueue.EnqueueAudit(
                           userId,
                           dto.CompanyId,
                           AuditAction.Create,
                           $"Warehouse '{dto.Name}' registered with admin '{dto.CompanyId}'."
                       );
            }
            catch { }

            // Invalidate cache for company warehouses
            _cache.RemoveByPrefix($"warehouses_company_{dto.CompanyId}");

            return Result<Guid>.SuccessResponse(warehouse.Id);
        }

        public async Task<Result<string>> DeleteWarehouse(Guid warehouseId)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
                return Result<string>.FailureResponse("Warehouse not found");

            warehouse.MarkAsDeleted();

            try
            {
                await _context.SaveChangesAsync();

                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();

                if (companyId.HasValue)
                    _jobqueue.EnqueueAudit(userId, companyId.Value, AuditAction.Delete,
                        $"Warehouse '{warehouse.Name}' marked as deleted");

                _cache.Remove($"warehouse_{warehouse.Id}");
                _cache.RemoveByPrefix($"warehouses_company_{companyId}");
            }
            catch
            {
                return Result<string>.FailureResponse("Error deleting warehouse");
            }

            return Result<string>.SuccessResponse("Warehouse deleted successfully");
        }

        public async Task<Result<WarehouseDto>> GetWarehouseById(Guid warehouseId)
        {
            if (warehouseId == Guid.Empty)
                return Result<WarehouseDto>.FailureResponse("Invalid warehouse ID");

            var cacheKey = $"warehouse_{warehouseId}";
            var warehouse = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _context.Warehouses
                    .Where(w => w.Id == warehouseId)
                    .Select(w => new WarehouseDto
                    {
                        Id = w.Id,
                        Name = w.Name,
                        Location = w.Location,
                        CompanyId = w.CompanyId
                    })
                    .FirstOrDefaultAsync();
            });

            if (warehouse == null)
                return Result<WarehouseDto>.FailureResponse("Warehouse not found");

            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();
            if (companyId.HasValue)
                _jobqueue.EnqueueAudit(userId, companyId.Value, AuditAction.Read,
                    $"Fetched warehouse with ID {warehouseId}");

            return Result<WarehouseDto>.SuccessResponse(warehouse);
        }

        public async Task<Result<List<WarehouseDto>>> GetWarehouses(Guid companyId, int pageNumber, int pageSize)
        {
            if (companyId == Guid.Empty)
                return Result<List<WarehouseDto>>.FailureResponse("Invalid company ID");

            var cacheKey = $"warehouses_company_{companyId}_page_{pageNumber}_size_{pageSize}";
            var warehouses = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _context.Warehouses
                    .Where(w => w.CompanyId == companyId)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(w => new WarehouseDto
                    {
                        Id = w.Id,
                        Name = w.Name,
                        Location = w.Location,
                        CompanyId = w.CompanyId
                    })
                    .ToListAsync();
            });

            if (warehouses == null || !warehouses.Any())
                return Result<List<WarehouseDto>>.FailureResponse("No warehouses found");

            var userId = _currentUserService.GetCurrentUserId();
            _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read,
                $"Fetched warehouses for company ID {companyId}, page {pageNumber}");

            return Result<List<WarehouseDto>>.SuccessResponse(warehouses);
        }

        public async Task<Result<List<WarehouseDto>>> GetWarehousesContainingProduct(Guid productId)
        {
            if (productId == Guid.Empty)
                return Result<List<WarehouseDto>>.FailureResponse("Invalid product ID");

            var cacheKey = $"warehouses_product_{productId}";
            var warehouses = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _context.Warehouses
                    .Include(w => w.ProductWarehouses)
                    .Where(w => w.ProductWarehouses.Any(pw => pw.ProductId == productId))
                    .Select(w => new WarehouseDto
                    {
                        Id = w.Id,
                        Name = w.Name,
                        Location = w.Location,
                        CompanyId = w.CompanyId
                    })
                    .ToListAsync();
            });

            if (warehouses == null || !warehouses.Any())
                return Result<List<WarehouseDto>>.FailureResponse("No warehouses found for this product");

            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();
            if (companyId.HasValue)
                _jobqueue.EnqueueAudit(userId, companyId.Value, AuditAction.Read,
                    $"Fetched warehouses containing product ID {productId}");

            return Result<List<WarehouseDto>>.SuccessResponse(warehouses);
        }

        public async Task<Result<string>> MarkAsActive(Guid warehouseId)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null) return Result<string>.FailureResponse("Warehouse not found");
            if (warehouse.IsActive) return Result<string>.FailureResponse("Warehouse is already active");

            warehouse.MarkAsActive();

            try
            {
                await _context.SaveChangesAsync();
                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();

                if (companyId.HasValue)
                    _jobqueue.EnqueueAudit(userId, companyId.Value, AuditAction.Update,
                        $"Warehouse '{warehouse.Name}' marked as active");

                _cache.Remove($"warehouse_{warehouse.Id}");
                _cache.RemoveByPrefix($"warehouses_company_{companyId}");
            }
            catch
            {
                return Result<string>.FailureResponse("Error marking warehouse as active");
            }

            return Result<string>.SuccessResponse("Warehouse marked as active successfully");
        }

        public async Task<Result<string>> MarkAsInActive(Guid warehouseId)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null) return Result<string>.FailureResponse("Warehouse not found");
            if (!warehouse.IsActive) return Result<string>.FailureResponse("Warehouse is already inactive");

            warehouse.MarkAsInActive();

            try
            {
                await _context.SaveChangesAsync();
                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();

                if (companyId.HasValue)
                    _jobqueue.EnqueueAudit(userId, companyId.Value, AuditAction.Update,
                        $"Warehouse '{warehouse.Name}' marked as inactive");

                _cache.Remove($"warehouse_{warehouse.Id}");
                _cache.RemoveByPrefix($"warehouses_company_{companyId}");
            }
            catch
            {
                return Result<string>.FailureResponse("Error marking warehouse as inactive");
            }

            return Result<string>.SuccessResponse("Warehouse marked as inactive successfully");
        }

        public async Task<Result<string>> UpdateWarehouse(Guid warehouseId, UpdateWarehouseDto dto)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null) return Result<string>.FailureResponse("Warehouse not found");

            try
            {
                await _phoneValidator.Validate(dto.PhoneNumber);

                warehouse.Name = dto.Name;
                warehouse.Location = dto.Location;
                warehouse.PhoneNumber = dto.PhoneNumber;
                warehouse.MarkAsUpdated();

                await _context.SaveChangesAsync();

                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();

                if (companyId.HasValue)
                    _jobqueue.EnqueueAudit(userId, companyId.Value, AuditAction.Update,
                        $"Warehouse '{warehouse.Name}' updated successfully");

                _cache.Remove($"warehouse_{warehouse.Id}");
                _cache.RemoveByPrefix($"warehouses_company_{companyId}");
            }
            catch (ValidationException)
            {
                return Result<string>.FailureResponse("Invalid phone number");
            }
            catch
            {
                return Result<string>.FailureResponse("Error updating warehouse");
            }

            return Result<string>.SuccessResponse("Warehouse updated successfully");
        }
    }
}
