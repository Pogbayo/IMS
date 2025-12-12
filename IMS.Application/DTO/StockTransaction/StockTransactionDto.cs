
using IMS.Domain.Enums;

namespace IMS.Application.DTO.StockTransaction
{
    public class StockTransactionDto
    {
        public int QuantityChanged { get; set; }
        public TransactionType Type { get; set; }
        public string? Note { get; set; }
        public DateTime TransactionDate { get; set; }
        public int NewStockLevel { get; set; }
        public Guid WarehouseId { get; set; }
    }
}
