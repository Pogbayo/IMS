
using IMS.Domain.Enums;

namespace IMS.Application.DTO.StockTransaction
{
    public class SaleStockLevelDto
    {
        public int PreviousQuantity { get; set; }
        public int NewQuantity { get; set; }
    }

    public class PurchaseStockLevelDto
    {
        public int PreviousQuantity { get; set; }
        public int NewQuantity { get; set; }
    }

    public class TransferStockLevelDto
    {
        public string? FromWarehouseName { get; set; }
        public string? ToWarehouseName { get; set; }
        public int FromWarehousePreviousQuantity { get; set; }
        public int FromWarehouseNewQuantity { get; set; }
        public int ToWarehousePreviousQuantity { get; set; }
        public int ToWarehouseNewQuantity { get; set; }
    }

    public class StockTransactionDto
    {
        public int QuantityChanged { get; set; }
        public TransactionType Type { get; set; }
        public string? Note { get; set; }
        public DateTime TransactionDate { get; set; }
        public Object? NewStockLevel { get; set; }
        public Guid WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
    }
}
