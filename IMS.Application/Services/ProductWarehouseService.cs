using IMS.Application.DTO.StockTransaction;
using IMS.Application.Helpers;
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
            var productWarehouse = await _context.ProductWarehouses
                .Include(pw => pw.Product)
                .FirstOrDefaultAsync(pw => pw.ProductId == productId && pw.WarehouseId == warehouseId);

            if (productWarehouse == null)
            {
                _logger.LogWarning("Product {ProductId} not found in warehouse {WarehouseId}", productId, warehouseId);
                throw new InvalidOperationException("Product not found in the warehouse.");
            }

            int adjustedQuantity = updateType == ProductUpdateInWarehouseType.IncreaseQuantity
                ? quantityChanged
                : -quantityChanged;

            if (productWarehouse.Quantity + adjustedQuantity < 0)
                throw new InvalidOperationException("Not enough quantity to decrease.");

            productWarehouse.Quantity += adjustedQuantity;
            await _context.SaveChangesAsync();

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
                CompanyId = productWarehouse.Product!.CompanyId
            };

            await _stockTransactionService.LogTransaction(stockDto);

            var description = $"{_currentUserService.GetCurrentUserId()} " +
                              $"{(updateType == ProductUpdateInWarehouseType.IncreaseQuantity ? "added" : "removed")} " +
                              $"{quantityChanged} units of product {productId} in warehouse {warehouseId}";

            _jobqueue.EnqueueAudit(userId, productWarehouse.Product!.CompanyId, AuditAction.Update, description);
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

            var transferDto = new CreateStockTransactionDto
            {
                ProductId = productId,
                FromWarehouseId = fromWarehouseId,
                ToWarehouseId = toWarehouseId,
                QuantityChanged = quantity,
                Type = TransactionType.Transfer,
                Note = $"Transfer of {quantity} units from warehouse {fromWarehouseId} to {toWarehouseId}",
                CompanyId = product!.CompanyId
            };

            await _stockTransactionService.LogTransaction(transferDto);

            var description = $"{_currentUserService.GetCurrentUserId()} transferred {quantity} units of product {productId} " +
                              $"from warehouse {fromWarehouseId} to warehouse {toWarehouseId}";

            _jobqueue.EnqueueAudit(userId, product!.CompanyId, AuditAction.Transfer, description);
            _jobqueue.EnqueueCloudWatchAudit(description);
        }
    }
}
