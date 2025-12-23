using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services
{
    public class ProductWarehouseService : IProductWarehouseService
    {
        private readonly ILogger<ProductWarehouseService> _logger;
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUserService;

        public ProductWarehouseService(
            ILogger<ProductWarehouseService> logger,
            IAppDbContext context,
            IAuditService audit,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _context = context;
            _audit = audit;
            _currentUserService = currentUserService;
        }

        public async Task UpdateProductInWarehouseAsync(
            Guid productId,
            Guid warehouseId,
            int quantityChanged,
            Guid userId,
            ProductUpdateInWarehouseType updateType)
        {
            var productWarehouse = await _context.ProductWarehouses
                .Include(pw => pw.Product)
                .FirstOrDefaultAsync(pw => pw.ProductId == productId && pw.WarehouseId == warehouseId);

            if (productWarehouse == null)
            {
                _logger.LogWarning("Product {ProductId} not found in warehouse {WarehouseId}", productId, warehouseId);
                throw new InvalidOperationException("Product not found in the warehouse.");
            }

            if (updateType == ProductUpdateInWarehouseType.IncreaseQuantity)
                productWarehouse.Quantity += quantityChanged;
            else
            {
                if (productWarehouse.Quantity < quantityChanged)
                    throw new InvalidOperationException("Not enough quantity to decrease.");
                productWarehouse.Quantity -= quantityChanged;
            }

            await _context.SaveChangesAsync();

            var description = $"{_currentUserService.GetCurrentUserId()} " +
                              $"{(updateType == ProductUpdateInWarehouseType.IncreaseQuantity ? "added" : "removed")} " +
                              $"{quantityChanged} units of product {productId} in warehouse {warehouseId}";

            await _audit.LogAsync(userId, productWarehouse.Product!.CompanyId, AuditAction.Update, description);

            _logger.LogInformation("Product {ProductId} in warehouse {WarehouseId} updated successfully", productId, warehouseId);
        }

        public async Task TransferProductAsync(
            Guid productId,
            Guid fromWarehouseId,
            Guid toWarehouseId,
            int quantity,
            Guid userId)
        {
            if (fromWarehouseId == toWarehouseId)
                throw new InvalidOperationException("Cannot transfer to the same warehouse.");

            await UpdateProductInWarehouseAsync(
                productId,
                fromWarehouseId,
                quantity,
                userId,
                ProductUpdateInWarehouseType.DecreaseQuantity
            );

            await UpdateProductInWarehouseAsync(
                productId,
                toWarehouseId,
                quantity,
                userId,
                ProductUpdateInWarehouseType.IncreaseQuantity
            );

            _logger.LogInformation("Transferred {Quantity} units of product {ProductId} from warehouse {FromWarehouse} to {ToWarehouse}",
                quantity, productId, fromWarehouseId, toWarehouseId);

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);

            var description = $"{_currentUserService.GetCurrentUserId()} transferred {quantity} units of product {productId} " +
                              $"from warehouse {fromWarehouseId} to warehouse {toWarehouseId}";

            await _audit.LogAsync(userId, product!.CompanyId, AuditAction.Transfer, description);
        }
    }
}
