using IMS.Application.DTO.StockTransaction;
using IMS.Application.Interfaces;

namespace IMS.Application.Services
{
    internal class StockTransactionService : IStockTransactionService
    {
        public Task<List<StockTransactionDto>> GetStockHistory(Guid productId)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> LogTransaction(CreateStockTransactionDto dto)
        {
            throw new NotImplementedException();
        }
    }
}
