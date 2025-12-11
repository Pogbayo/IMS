using IMS.Application.ApiResponse;
using IMS.Application.DTO;
using IMS.Application.DTO.Warehouse;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

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

        public WarehouseService(UserManager<AppUser> usermanager,
                                ILogger<CompanyService> logger,
                                IAppDbContext context,
                                IAuditService audit,
                                ICurrentUserService currentUserService,
                                IPhoneValidator phonevalidator)
        {
            _logger = logger;
            _context = context;
            _audit = audit;
            _currentUserService = currentUserService;
            _phoneValidator = phonevalidator;
            _userManager = usermanager;
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
            {
                _logger.LogWarning("CreateWarehouse called with null DTO");
                return Result<Guid>.FailureResponse("Bad Request: DTO cannot be null");
            }

            try
            {
                await _phoneValidator.Validate(dto.PhoneNumber);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning("Invalid phone number for warehouse: {PhoneNumber}", dto.PhoneNumber);
                _logger.LogCritical(ex.Message);
                return Result<Guid>.FailureResponse("Invalid phone number");
            }

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
                {
                    _logger.LogError("No records were saved when creating warehouse");
                    return Result<Guid>.FailureResponse("Warehouse could not be saved");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving warehouse to database");
                return Result<Guid>.FailureResponse("Error saving warehouse");
            }

            try
            {
                var userId = _currentUserService.GetCurrentUserId();
                await _audit.LogAsync(
                    userId,
                    dto.CompanyId,
                    AuditAction.Create,
                    $"Warehouse '{warehouse.Name}' created successfully"
                );
                _logger.LogInformation("Warehouse {WarehouseName} created and audit logged", warehouse.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Warehouse created but audit logging failed for warehouse {WarehouseName}", warehouse.Name);
            }

            return Result<Guid>.SuccessResponse(warehouse.Id);
        }

        public async Task<Result<string>> DeleteWarehouse(Guid warehouseId)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
            {
                _logger.LogWarning("Attempted to delete non-existing warehouse with ID {WarehouseId}", warehouseId);
                return Result<string>.FailureResponse("Warehouse not found");
            }

            warehouse.MarkAsDeleted();

            try
            {
                await _context.SaveChangesAsync();

                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();

                if (companyId.HasValue)
                {
                    await _audit.LogAsync(
                        userId,
                        companyId.Value,
                        AuditAction.Delete,
                        $"Warehouse '{warehouse.Name}' marked as deleted"
                    );
                }

                _logger.LogInformation("Warehouse {WarehouseName} marked as deleted", warehouse.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting warehouse with ID {WarehouseId}", warehouseId);
                return Result<string>.FailureResponse("Error deleting warehouse");
            }

            return Result<string>.SuccessResponse("Warehouse deleted successfully");
        }

        public async Task<Result<WarehouseDto>> GetWarehouseById(Guid warehouseId)
        {
            if (warehouseId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to fetch warehouse with an empty ID");
                return Result<WarehouseDto>.FailureResponse("Invalid warehouse ID");
            }

            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            try
            {
                if (companyId.HasValue)
                {
                    await _audit.LogAsync(
                        userId,
                        companyId.Value,
                        AuditAction.Read,
                        $"Fetched warehouse with ID {warehouseId}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit for fetching warehouse ID {WarehouseId}", warehouseId);
            }

            try
            {
                var warehouse = await _context.Warehouses
                    .Where(w => w.Id == warehouseId)
                    .Select(w => new WarehouseDto
                    {
                        Id = w.Id,
                        Name = w.Name,
                        Location = w.Location,
                        CompanyId = w.CompanyId
                    })
                    .FirstOrDefaultAsync();

                if (warehouse == null)
                {
                    _logger.LogInformation("Warehouse not found with ID {WarehouseId}", warehouseId);
                    return Result<WarehouseDto>.FailureResponse("Warehouse not found");
                }

                return Result<WarehouseDto>.SuccessResponse(warehouse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching warehouse with ID {WarehouseId}", warehouseId);
                return Result<WarehouseDto>.FailureResponse("An error occurred while fetching the warehouse");
            }
        }

        public async Task<Result<List<WarehouseDto>>> GetWarehouses(Guid companyId, int pageNumber, int pageSize)
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to fetch warehouses with an empty company ID");
                return Result<List<WarehouseDto>>.FailureResponse("Invalid company ID");
            }

            if (pageNumber <= 0 || pageSize <= 0)
            {
                _logger.LogWarning("Invalid pagination parameters: PageNumber={PageNumber}, PageSize={PageSize}", pageNumber, pageSize);
                return Result<List<WarehouseDto>>.FailureResponse("Invalid pagination parameters");
            }

            var userId = _currentUserService.GetCurrentUserId();
            var currentUserCompanyId = await GetCurrentUserCompanyIdAsync();

            try
            {
                if (currentUserCompanyId.HasValue)
                {
                    await _audit.LogAsync(
                        userId,
                        currentUserCompanyId.Value,
                        AuditAction.Read,
                        $"Fetched warehouses for company with ID {companyId}, page {pageNumber}, size {pageSize}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit for fetching warehouses for company ID {CompanyId}", companyId);
            }

            try
            {
                var warehouses = await _context.Warehouses
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

                if (!warehouses.Any())
                {
                    _logger.LogInformation("No warehouses found for company ID {CompanyId}", companyId);
                    return Result<List<WarehouseDto>>.FailureResponse("No warehouses found");
                }

                return Result<List<WarehouseDto>>.SuccessResponse(warehouses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching warehouses for company ID {CompanyId}", companyId);
                return Result<List<WarehouseDto>>.FailureResponse("An error occurred while fetching warehouses");
            }
        }

        public async Task<Result<List<WarehouseDto>>> GetWarehousesContainingProduct(Guid productId)
        {
            if (productId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to fetch warehouses with an empty product ID");
                return Result<List<WarehouseDto>>.FailureResponse("Invalid product ID");
            }

            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            try
            {
                if (companyId.HasValue)
                {
                    await _audit.LogAsync(
                        userId,
                        companyId.Value,
                        AuditAction.Read,
                        $"Fetched warehouses containing product with ID {productId}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit for fetching warehouses with product ID {ProductId}", productId);
            }

            try
            {
                var warehouses = await _context.Warehouses
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

                if (!warehouses.Any())
                {
                    _logger.LogInformation("No warehouses found containing product with ID {ProductId}", productId);
                    return Result<List<WarehouseDto>>.FailureResponse("No warehouses found for this product");
                }

                return Result<List<WarehouseDto>>.SuccessResponse(warehouses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching warehouses containing product with ID {ProductId}", productId);
                return Result<List<WarehouseDto>>.FailureResponse("An error occurred while fetching warehouses");
            }
        }

        public async Task<Result<string>> MarkAsActive(Guid warehouseId)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
            {
                _logger.LogWarning("Attempted to mark a non-existing warehouse with ID {WarehouseId} as active", warehouseId);
                return Result<string>.FailureResponse("Warehouse not found");
            }

            if (warehouse.IsActive)
            {
                _logger.LogWarning("Warehouse {WarehouseName} is already active", warehouse.Name);
                return Result<string>.FailureResponse("Warehouse is already active");
            }

            warehouse.MarkAsActive();

            try
            {
                await _context.SaveChangesAsync();
                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();

                if (companyId.HasValue)
                {
                    await _audit.LogAsync(
                        userId,
                        companyId.Value,
                        AuditAction.Update,
                        $"Warehouse '{warehouse.Name}' marked as active"
                    );
                }

                _logger.LogInformation("Warehouse {WarehouseName} marked as active", warehouse.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking warehouse with ID {WarehouseId} as active", warehouseId);
                return Result<string>.FailureResponse("Error marking warehouse as active");
            }

            return Result<string>.SuccessResponse("Warehouse marked as active successfully");
        }

        public async Task<Result<string>> MarkAsInActive(Guid warehouseId)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
            {
                _logger.LogWarning("Attempted to mark a non-existing warehouse with ID {WarehouseId} as inactive", warehouseId);
                return Result<string>.FailureResponse("Warehouse not found");
            }

            if (!warehouse.IsActive)
            {
                _logger.LogWarning("Warehouse {WarehouseName} is already inactive", warehouse.Name);
                return Result<string>.FailureResponse("Warehouse is already inactive");
            }

            warehouse.MarkAsInActive();

            try
            {
                await _context.SaveChangesAsync();
                var userId = _currentUserService.GetCurrentUserId();
                var companyId = await GetCurrentUserCompanyIdAsync();

                if (companyId.HasValue)
                {
                    await _audit.LogAsync(
                        userId,
                        companyId.Value,
                        AuditAction.Update,
                        $"Warehouse '{warehouse.Name}' marked as inactive"
                    );
                }

                _logger.LogInformation("Warehouse {WarehouseName} marked as inactive", warehouse.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking warehouse with ID {WarehouseId} as inactive", warehouseId);
                return Result<string>.FailureResponse("Error marking warehouse as inactive");
            }

            return Result<string>.SuccessResponse("Warehouse marked as inactive successfully");
        }

        public async Task<Result<string>> UpdateWarehouse(Guid warehouseId, UpdateWarehouseDto dto)
        {
            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
            {
                _logger.LogWarning("Attempted to update non-existing warehouse with ID {WarehouseId}", warehouseId);
                return Result<string>.FailureResponse("Warehouse not found");
            }

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
                {
                    await _audit.LogAsync(
                        userId,
                        companyId.Value,
                        AuditAction.Update,
                        $"Warehouse '{warehouse.Name}' updated successfully"
                    );
                }

                _logger.LogInformation("Warehouse {WarehouseName} updated", warehouse.Name);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Phone number validation failed for warehouse {WarehouseName}", dto.Name);
                return Result<string>.FailureResponse("Invalid phone number");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating warehouse with ID {WarehouseId}", warehouseId);
                return Result<string>.FailureResponse("Error updating warehouse");
            }

            return Result<string>.SuccessResponse("Warehouse updated successfully");
        }
    }
}
