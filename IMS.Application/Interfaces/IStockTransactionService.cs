using IMS.Application.ApiResponse;
using IMS.Application.DTO.StockTransaction;
using IMS.Domain.Enums;
namespace IMS.Application.Interfaces
{
    public interface IStockTransactionService
    {
        Task<Result<List<StockTransactionDto>>> GetStockTransactions(
               Guid companyId,
               DateTime? fromDate = null,
               DateTime? toDate = null,
               Guid? productId = null,
               Guid? fromWarehouseId = null,
               Guid? toWarehouseId = null,
               TransactionType? transactionType = null,
               int pageNumber = 1,
               int pageSize = 20
           );

//        pageNumber = max(pageNumber, 1)
//pageSize = min(pageSize, MAX_PAGE_SIZE)
        Task<Result<bool>> LogTransaction(CreateStockTransactionDto dto);
    }
}
