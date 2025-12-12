using IMS.Application.ApiResponse;
using IMS.Application.DTO.Product;
using Microsoft.AspNetCore.Http;


namespace IMS.Application.Interfaces
{
    public interface IProductService
    {
        Task<Result<Guid>> CreateProduct(ProductCreateDto dto);
        Task<Result<ProductDto>> GetProductById(Guid productId);
        Task<Result<List<dynamic>>> GetProductsByCompanyId(Guid companyId);
        Task<Result<string>> UpdateProduct(Guid productId, ProductUpdateDto dto);
        Task<Result<string>> DeleteProduct(Guid productId);
        Task<Result<List<ProductDto>>> GetProductsInWarehouse(Guid warehouseId);
        Task<Result<string>> UploadProductImage(Guid productId, IFormFile file);
        Task<Result<ProductDto>> GetProductBySku(string sku);
        Task<Result<List<ProductDto>>> GetFilteredProducts(
            Guid? warehouseId = null,
            Guid? supplierId = null,
            int pageNumber = 1,
            int pageSize = 20
        );
    }
}
