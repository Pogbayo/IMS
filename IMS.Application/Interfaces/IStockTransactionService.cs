using IMS.Application.DTO.StockTransaction;
namespace IMS.Application.Interfaces
{
    public interface IStockTransactionService
    {
        Task<List<StockTransactionDto>> GetStockHistory(Guid productId);
        Task<Guid> LogTransaction(CreateStockTransactionDto dto);
    }
}
