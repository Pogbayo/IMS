using IMS.Application.DTO.StockTransaction;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
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
        private readonly IJobQueue _jobqueue;
        private readonly IStockTransactionService _stockTransactionService;

        public ProductWarehouseService(
            IJobQueue jobqueue,
            ILogger<ProductWarehouseService> logger,
            IAppDbContext context,
            IAuditService audit,
            ICurrentUserService currentUserService,
            IStockTransactionService stockTransactionService)
        {
            _jobqueue = jobqueue;
            _logger = logger;
            _context = context;
            _audit = audit;
            _currentUserService = currentUserService;
            _stockTransactionService = stockTransactionService;
        }

        public async Task UpdateProductInWarehouseAsync(
            Guid productId,
            Guid warehouseId,
            int quantityChanged,
            Guid userId,
            ProductUpdateInWarehouseType updateType)
        {
            if (quantityChanged <= 0)
                throw new InvalidOperationException("Quantity changed must be positive.");

            // Shared query: Always attempt to find existing ProductWarehouse
            var productWarehouse = await _context.ProductWarehouses
                .Include(pw => pw.Product)
                .FirstOrDefaultAsync(pw => pw.ProductId == productId && pw.WarehouseId == warehouseId);

            Product product; // For CompanyId in DTO/audit

            if (productWarehouse == null)
            {
                if (updateType == ProductUpdateInWarehouseType.DecreaseQuantity)
                {
                    _logger.LogWarning("Product {ProductId} not found in warehouse {WarehouseId}", productId, warehouseId);
                    throw new InvalidOperationException("Product not found in the warehouse.");
                }

                // For IncreaseQuantity: Create new record
                // Fetch Product to get CompanyId and set navigation
                product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == productId)
                    ?? throw new InvalidOperationException("Product not found.");

                productWarehouse = new ProductWarehouse
                {
                    Id = Guid.NewGuid(), // Assuming BaseEntity has Id as Guid
                    ProductId = productId,
                    Product = product,
                    WarehouseId = warehouseId,
                    Quantity = 0 // Will be set to quantityChanged below
                };

                _context.ProductWarehouses.Add(productWarehouse);
                _logger.LogInformation("Created new ProductWarehouse for {ProductId} in {WarehouseId}", productId, warehouseId);
            }
            else
            {
                product = productWarehouse.Product!;
            }

            // Shared adjustment logic
            int adjustedQuantity = updateType == ProductUpdateInWarehouseType.IncreaseQuantity
                ? quantityChanged
                : -quantityChanged;

            if (updateType == ProductUpdateInWarehouseType.DecreaseQuantity && productWarehouse.Quantity + adjustedQuantity < 0)
                throw new InvalidOperationException("Not enough quantity to decrease.");

            // For new records (increase), this sets Quantity to quantityChanged; for existing, adjusts
            productWarehouse.Quantity += adjustedQuantity;

            await _context.SaveChangesAsync();

            // Transaction logging (inner call logs as Purchase/Sale; outer handles Transfer)
            var transactionType = updateType == ProductUpdateInWarehouseType.IncreaseQuantity
                ? TransactionType.Purchase
                : TransactionType.Sale;

            var stockDto = new CreateStockTransactionDto
            {
                ProductId = productId,
                FromWarehouseId = updateType == ProductUpdateInWarehouseType.DecreaseQuantity ? warehouseId : Guid.Empty,
                ToWarehouseId = updateType == ProductUpdateInWarehouseType.IncreaseQuantity ? warehouseId : Guid.Empty,
                QuantityChanged = quantityChanged,
                Type = transactionType,
                Note = $"{updateType} of {quantityChanged} units",
                CompanyId = product.CompanyId
            };
            await _stockTransactionService.LogTransaction(stockDto);

            // Audit
            var description = $"{_currentUserService.GetCurrentUserId()} " +
                              $"{(updateType == ProductUpdateInWarehouseType.IncreaseQuantity ? "added" : "removed")} " +
                              $"{quantityChanged} units of product {productId} in warehouse {warehouseId}";
            _jobqueue.EnqueueAudit(userId, product.CompanyId, AuditAction.Update, description);
            _jobqueue.EnqueueCloudWatchAudit(description);

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

            if (quantity <= 0)
                throw new InvalidOperationException("Quantity must be positive.");

            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();
            try
            {
                // Decrease from source
                await UpdateProductInWarehouseAsync(
                    productId,
                    fromWarehouseId,
                    quantity,
                    userId,
                    ProductUpdateInWarehouseType.DecreaseQuantity
                );

                // Increase to target (creates if needed)
                await UpdateProductInWarehouseAsync(
                    productId,
                    toWarehouseId,
                    quantity,
                    userId,
                    ProductUpdateInWarehouseType.IncreaseQuantity
                );

                // Commit DB changes
                await transaction.CommitAsync();

                _logger.LogInformation("Transferred {Quantity} units of product {ProductId} from warehouse {FromWarehouse} to {ToWarehouse}",
                    quantity, productId, fromWarehouseId, toWarehouseId);

                // Fetch product for CompanyId
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId)
                    ?? throw new InvalidOperationException("Product not found.");

                // Log transfer-specific transaction (overrides inner Purchase/Sale logs if needed)
                var transferDto = new CreateStockTransactionDto
                {
                    ProductId = productId,
                    FromWarehouseId = fromWarehouseId,
                    ToWarehouseId = toWarehouseId,
                    QuantityChanged = quantity,
                    Type = TransactionType.Transfer,
                    Note = $"Transfer of {quantity} units from warehouse {fromWarehouseId} to {toWarehouseId}",
                    CompanyId = product.CompanyId
                };
                await _stockTransactionService.LogTransaction(transferDto);

                // Transfer-specific audit
                var description = $"{_currentUserService.GetCurrentUserId()} transferred {quantity} units of product {productId} " +
                                  $"from warehouse {fromWarehouseId} to warehouse {toWarehouseId}";
                _jobqueue.EnqueueAudit(userId, product.CompanyId, AuditAction.Transfer, description);
                _jobqueue.EnqueueCloudWatchAudit(description);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw; 
            }
        }
    }
}