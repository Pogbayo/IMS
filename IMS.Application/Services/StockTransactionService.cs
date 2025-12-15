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
        public StockTransactionService(IAuditService auditService,IProductService productService,IAppDbContext context, ILogger<StockTransactionService> logger, ICurrentUserService currentUserService)
        {
            _productService = productService;
            _auditservice = auditService;
            _context = context;
            _logger = logger;
            _currentUserService = currentUserService;
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

            var userId = _currentUserService.GetCurrentUserId();

            IQueryable<StockTransaction> query = _context.StockTransactions
                .Where(q => q.CompanyId == companyId);

            if (fromDate.HasValue)
                query = query.Where(t => t.TransactionDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(t => t.TransactionDate <= toDate.Value);

            if (productId.HasValue)
                query = query.Where(t => t.ProductWarehouse!.ProductId == productId);

            if (fromWarehouseId.HasValue)
                query = query.Where(t => t.FromWarehouseId == fromWarehouseId);

            if (toWarehouseId.HasValue)
                query = query.Where(t => t.ToWarehouseId == toWarehouseId);

            if (transactionType.HasValue)
                query = query.Where(t => t.Type == transactionType);

            var stockTransactions = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                //.Select(st => new StockTransactionDto
                //{
                //    QuantityChanged = st.QuantityChanged,
                //    Type = st.Type,
                //    Note = st.Note,
                //    TransactionDate = st.TransactionDate,
                //})
                .ToListAsync();

            var transactions = new List<StockTransactionDto>();

            foreach (var st in stockTransactions)
            {
                object newStockData;

                if (st.Type == TransactionType.Transfer)
                {
                    var result = _productService.CalculateNewStockDetails
                        (
                        st.Type,
                        st.ProductWarehouse!.Quantity,
                        st.QuantityChanged,
                        st.FromWarehouse!,
                        st.ToWarehouse!
                        );
                    newStockData = result;
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

                var stockTransaction = new StockTransactionDto
                {
                    QuantityChanged = st.QuantityChanged,
                    Type = st.Type,
                    Note = st.Note,
                    TransactionDate = st.TransactionDate,
                    NewStockLevel = newStockData,
                    WarehouseId = st.ProductWarehouse!.WarehouseId,
                    WarehouseName = st.ProductWarehouse!.Warehouse!.Name
                };
                transactions.Add(stockTransaction);
            }

            if (transactions.Count == 0)
            {
                _logger.LogWarning("No Transaction record matched the filter...");
                return Result<List<StockTransactionDto>>.FailureResponse("No Transaction record matched the filter..");
            }

            await _auditservice.LogAsync(
                userId,
                companyId,
                AuditAction.Read,
                $"User: {userId} fetched Inventory Movements in the Company"
                );

            return Result<List<StockTransactionDto>>.SuccessResponse(transactions, "Filtered transactions record retrieved successfully..");
        }

        public async Task<Result<bool>> LogTransaction(CreateStockTransactionDto dto)
        {
            _logger.LogInformation("Attempting to log new stock transaction for Product: {ProductId}", dto.ProductId);

            var userId = _currentUserService.GetCurrentUserId();

            if (dto.ProductId == Guid.Empty)
            {
                const string errorMessage = "ProductWarehouseId cannot be empty.";
                _logger.LogWarning(errorMessage);
                return Result<bool>.FailureResponse(errorMessage);
            }

            if (dto.QuantityChanged <= 0)
            {
                const string errorMessage = "Quantity changed must be a positive integer.";
                _logger.LogWarning(errorMessage);
                return Result<bool>.FailureResponse(errorMessage);
            }

            if (dto.Type <= 0)
            {
                const string errorMessage = "Invalid Transaction Type provided.";
                _logger.LogWarning(errorMessage);
                return Result<bool>.FailureResponse(errorMessage);
            }

            var company = await _context.Companies.FindAsync(dto.CompanyId);
            if (company == null)
            {
                const string errorMessage = "Log needs to be attached to a valid Company.";
                _logger.LogWarning(errorMessage);
                return Result<bool>.FailureResponse(errorMessage);
            }

            using var dbTransaction = ((DbContext)_context).Database.BeginTransaction();

            List<StockTransaction> transactions = new List<StockTransaction>();

            var productWarehouseId = await _context.ProductWarehouses
                .Where(pw => pw.ProductId == dto.ProductId && pw.WarehouseId == dto.FromWarehouseId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            var productWarehouseIdForTransafer = await _context.ProductWarehouses
                .Where(pw => pw.ProductId == dto.ProductId && pw.WarehouseId == dto.ToWarehouseId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (productWarehouseId == Guid.Empty)
            {
                const string errorMessage = "ProductWarehouseId can not be null";
                _logger.LogWarning(errorMessage);
                return Result<bool>.FailureResponse(errorMessage);
            }

            if (productWarehouseIdForTransafer == Guid.Empty)
            {
                const string errorMessage = "ProductWarehouseId can not be null";
                _logger.LogWarning(errorMessage);
                return Result<bool>.FailureResponse(errorMessage);
            }

            if (dto.Type == TransactionType.Sale)
            {
                var result = new StockTransaction
                {
                    ProductWarehouseId = productWarehouseId,
                    QuantityChanged = -dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    FromWarehouseId = dto.FromWarehouseId,
                };
                transactions.Add(result);
            }
            else if (dto.Type == TransactionType.Purchase)
            {
                var result = new StockTransaction
                {
                    ProductWarehouseId = productWarehouseId,
                    QuantityChanged = dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    ToWarehouseId = dto.ToWarehouseId,
                };
                transactions.Add(result);
            }
            else if (dto.Type == TransactionType.Transfer)
            {
                var result = new StockTransaction
                {
                    ProductWarehouseId = productWarehouseId,
                    QuantityChanged = -dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    FromWarehouseId = dto.FromWarehouseId,
                };

                var result2 = new StockTransaction
                {
                    ProductWarehouseId = productWarehouseIdForTransafer,
                    QuantityChanged = +dto.QuantityChanged,
                    Type = dto.Type,
                    Note = dto.Note,
                    UserId = userId,
                    CompanyId = dto.CompanyId,
                    ToWarehouseId = dto.ToWarehouseId,
                };
                transactions.Add(result);
                transactions.Add(result2);
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
                bool isSaved;

                if (dbResult > 0)
                {
                    isSaved = true;
                    _logger.LogInformation("Logs saved successfully");
                    await _auditservice.LogAsync(userId, dto.CompanyId, AuditAction.Create, $"{userId} created a log for the following transaction");
                    return Result<bool>.SuccessResponse(isSaved, "Inventory Movement logged successfully...");
                }
                else
                {
                    _logger.LogWarning("Inventory Movement was not logged successfully...");
                    return Result<bool>.FailureResponse("Inventory Movement was not logged successfully...");
                }
            }
            catch (Exception)
            {
                _logger.LogWarning("An error occured while saving the logs");
                await dbTransaction.RollbackAsync();
                throw new Exception("An unknown error occured while saving log to the db");
            }
        }
    }
}
