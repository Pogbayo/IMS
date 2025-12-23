using IMS.Application.ApiResponse;
using IMS.Application.DTO.StockTransaction;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services
{
    internal class StockTransactionService : IStockTransactionService
    {
        private readonly IAppDbContext _context;
        private readonly ILogger<StockTransactionService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IProductService _productService;
        private readonly IAuditService _auditservice;
        private readonly ICustomMemoryCache _cache;

        public StockTransactionService(
            IAuditService auditService,
            Func<IProductService> productServiceFactory,
            IAppDbContext context,
            ILogger<StockTransactionService> logger,
            ICurrentUserService currentUserService,
            ICustomMemoryCache cache)
        {
            _productService = productServiceFactory();
            _auditservice = auditService;
            _context = context;
            _logger = logger;
            _currentUserService = currentUserService;
            _cache = cache;
        }

        public async Task<Result<List<StockTransactionDto>>> GetStockTransactions(
            Guid companyId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            Guid? productId = null,
            Guid? fromWarehouseId = null,
            Guid? toWarehouseId = null,
            TransactionType? transactionType = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            _logger.LogInformation("Getting the Stock transactions...");

            string cacheKey = $"StockTransactions_{companyId}_{fromDate}_{toDate}_{productId}_{fromWarehouseId}_{toWarehouseId}_{transactionType}_{pageNumber}_{pageSize}";

            var transactions = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                var userId = _currentUserService.GetCurrentUserId();

                IQueryable<StockTransaction> query = _context.StockTransactions
                    .Where(q => q.CompanyId == companyId);

                if (fromDate.HasValue) query = query.Where(t => t.TransactionDate >= fromDate.Value);
                if (toDate.HasValue) query = query.Where(t => t.TransactionDate <= toDate.Value);
                if (productId.HasValue) query = query.Where(t => t.ProductWarehouse!.ProductId == productId);
                if (fromWarehouseId.HasValue) query = query.Where(t => t.FromWarehouseId == fromWarehouseId);
                if (toWarehouseId.HasValue) query = query.Where(t => t.ToWarehouseId == toWarehouseId);
                if (transactionType.HasValue) query = query.Where(t => t.Type == transactionType);

                var stockTransactions = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var transactionDtos = new List<StockTransactionDto>();

                foreach (var st in stockTransactions)
                {
                    object newStockData;

                    if (st.Type == TransactionType.Transfer)
                    {
                        newStockData = _productService.CalculateNewStockDetails(
                            st.Type,
                            st.ProductWarehouse!.Quantity,
                            st.QuantityChanged,
                            st.FromWarehouse!,
                            st.ToWarehouse!
                        );
                    }
                    else if (st.Type == TransactionType.Sale)
                    {
                        newStockData = new SaleStockLevelDto
                        {
                            PreviousQuantity = st.ProductWarehouse!.Quantity,
                            NewQuantity = st.ProductWarehouse.Quantity - st.QuantityChanged
                        };
                    }
                    else if (st.Type == TransactionType.Purchase)
                    {
                        newStockData = new PurchaseStockLevelDto
                        {
                            PreviousQuantity = st.ProductWarehouse!.Quantity,
                            NewQuantity = st.ProductWarehouse.Quantity + st.QuantityChanged
                        };
                    }
                    else
                    {
                        newStockData = null!;
                    }

                    transactionDtos.Add(new StockTransactionDto
                    {
                        QuantityChanged = st.QuantityChanged,
                        Type = st.Type,
                        Note = st.Note,
                        TransactionDate = st.TransactionDate,
                        NewStockLevel = newStockData,
                        WarehouseId = st.ProductWarehouse!.WarehouseId,
                        WarehouseName = st.ProductWarehouse!.Warehouse!.Name
                    });
                }

                if (!transactionDtos.Any())
                {
                    _logger.LogWarning("No Transaction record matched the filter...");
                    return null; 
                }

                await _auditservice.LogAsync(userId, companyId, AuditAction.Read,
                    $"User: {userId} fetched Inventory Movements in the Company");

                return transactionDtos;
            });

            if (transactions == null || !transactions.Any())
                return Result<List<StockTransactionDto>>.FailureResponse("No Transaction record matched the filter..");

            return Result<List<StockTransactionDto>>.SuccessResponse(transactions, "Filtered transactions record retrieved successfully..");
        }

        public async Task<Result<bool>> LogTransaction(CreateStockTransactionDto dto)
        {
            _logger.LogInformation("Attempting to log new stock transaction for Product: {ProductId}", dto.ProductId);

            var userId = _currentUserService.GetCurrentUserId();

            if (dto.ProductId == Guid.Empty)
                return Result<bool>.FailureResponse("ProductWarehouseId cannot be empty.");

            if (dto.QuantityChanged <= 0)
                return Result<bool>.FailureResponse("Quantity changed must be a positive integer.");

            if (dto.Type <= 0)
                return Result<bool>.FailureResponse("Invalid Transaction Type provided.");

            var company = await _context.Companies.FindAsync(dto.CompanyId);
            if (company == null)
                return Result<bool>.FailureResponse("Log needs to be attached to a valid Company.");

            using var dbTransaction = ((DbContext)_context).Database.BeginTransaction();

            List<StockTransaction> transactions = new List<StockTransaction>();

            var productWarehouseId = await _context.ProductWarehouses
                .Where(pw => pw.ProductId == dto.ProductId && pw.WarehouseId == dto.FromWarehouseId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            var productWarehouseIdForTransfer = await _context.ProductWarehouses
                .Where(pw => pw.ProductId == dto.ProductId && pw.WarehouseId == dto.ToWarehouseId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (productWarehouseId == Guid.Empty || (dto.Type == TransactionType.Transfer && productWarehouseIdForTransfer == Guid.Empty))
                return Result<bool>.FailureResponse("ProductWarehouseId can not be null");

            if (dto.Type == TransactionType.Sale)
            {
                transactions.Add(new StockTransaction
                {
                    ProductWarehouseId = productWarehouseId,
                    QuantityChanged = -dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    FromWarehouseId = dto.FromWarehouseId,
                });
            }
            else if (dto.Type == TransactionType.Purchase)
            {
                transactions.Add(new StockTransaction
                {
                    ProductWarehouseId = productWarehouseId,
                    QuantityChanged = dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    ToWarehouseId = dto.ToWarehouseId,
                });
            }
            else if (dto.Type == TransactionType.Transfer)
            {
                transactions.Add(new StockTransaction
                {
                    ProductWarehouseId = productWarehouseId,
                    QuantityChanged = -dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    FromWarehouseId = dto.FromWarehouseId,
                });

                transactions.Add(new StockTransaction
                {
                    ProductWarehouseId = productWarehouseIdForTransfer,
                    QuantityChanged = dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    ToWarehouseId = dto.ToWarehouseId,
                });
            }
            else
            {
                return Result<bool>.FailureResponse("Please, provide a valid log Type...");
            }

            _context.StockTransactions.AddRange(transactions);

            try
            {
                var dbResult = await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                if (dbResult > 0)
                {
                    _logger.LogInformation("Logs saved successfully");
                    await _auditservice.LogAsync(userId, dto.CompanyId, AuditAction.Create,
                        $"{userId} created a log for the following transaction");

                    // Invalidtae cache for this company
                    _cache.RemoveByPrefix($"StockTransactions_{dto.CompanyId}_");

                    return Result<bool>.SuccessResponse(true, "Inventory Movement logged successfully...");
                }
                else
                {
                    _logger.LogWarning("Inventory Movement was not logged successfully...");
                    return Result<bool>.FailureResponse("Inventory Movement was not logged successfully...");
                }
            }
            catch (Exception)
            {
                _logger.LogWarning("An error occurred while saving the logs");
                await dbTransaction.RollbackAsync();
                throw new Exception("An unknown error occurred while saving log to the db");
            }
        }
    }
}
