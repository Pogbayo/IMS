namespace IMS.Application.DTO.StockTransaction
{
    public class CreateStockTransactionDto
    {
        public Guid ProductWarehouseId { get; set; }
        public int QuantityChanged { get; set; }
        public int Type { get; set; } 
        public string? Note { get; set; }
    }
}

