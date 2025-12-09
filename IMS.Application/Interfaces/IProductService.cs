using IMS.Application.DTO.Product;
using Microsoft.AspNetCore.Http;


namespace IMS.Application.Interfaces
{
    public interface IProductService
    {
        Task<Guid> CreateProduct(ProductCreateDto dto);
        Task<ProductDto> GetProductById(Guid productId);
        Task<List<ProductDto>> GetProducts(Guid companyId);
        Task UpdateProduct(Guid productId, ProductUpdateDto dto);
        Task DeleteProduct(Guid productId);
        Task<List<ProductDto>> GetProductsInWarehouse(Guid warehouseId);
        Task<string> UploadProductImage(Guid productId, IFormFile file);
    }
}
