namespace IMS.Application.DTO.Product
{
    public class ProductCreateDto
    {
        public required string Name { get; set; }
        public string? ImgUrl { get; set; }
        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public string? CategoryName { get; set; } 
        public Guid CompanyId { get; set; }
        //public Guid WarehouseId { get; set; }
        public int Quantity { get; set; }
        public Guid SupplierId { get; set; }
        public List<Guid> Warehouses { get; set; } = new List<Guid>();
    }
}