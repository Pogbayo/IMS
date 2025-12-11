using IMS.Application.ApiResponse;
using IMS.Application.DTO.Product;
using Microsoft.AspNetCore.Http;


namespace IMS.Application.Interfaces
{
    public interface IProductService
    {
        Task<Result<Guid>> CreateProduct(ProductCreateDto dto);
        Task<Result<ProductDto>> GetProductById(Guid productId);
        Task<Result<List<ProductDto>>> GetProducts(Guid companyId);
        Task<Result<string>> UpdateProduct(Guid productId, ProductUpdateDto dto);
        Task<Result<string>> DeleteProduct(Guid productId);
        Task<Result<List<ProductDto>>> GetProductsInWarehouse(Guid warehouseId);
        Task<Result<string>> UploadProductImage(Guid productId, IFormFile file);
    }
}
