
namespace IMS.Application.DTO.StockTransaction
{
    public class StockTransactionDto
    {
        public Guid Id { get; set; }
        public int QuantityChanged { get; set; }
        public int Type { get; set; }
        public string? Note { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}
