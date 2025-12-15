using IMS.Domain.Enums;

namespace IMS.Application.DTO.StockTransaction
{
    public class CreateStockTransactionDto
    {
        public Guid ProductId { get; set; }
        public int QuantityChanged { get; set; }
        public TransactionType Type { get; set; }
        public Guid CompanyId { get; set; }
        public string Note { get; set; } = string.Empty;
        public Guid? FromWarehouseId { get; set; }
        public Guid? ToWarehouseId { get; set; }
    }
}

