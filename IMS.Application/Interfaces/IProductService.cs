using IMS.Application.ApiResponse;
using IMS.Application.DTO.Product;
using IMS.Application.DTO.Warehouse;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace IMS.Application.Interfaces
{
    public interface IProductService
    {
        Task<Result<Guid>> CreateProduct(ProductCreateDto dto);
        Task<Result<ProductDto>> GetProductById(Guid productId);
        Task<Result<List<ProductsDto>>> GetProductsByCompanyId(Guid companyId , int pageSize, int pageNumber);
        Task<Result<string>> UpdateProduct(Guid productId, ProductUpdateDto dto);
        Task<Result<string>> DeleteProduct(Guid productId);
        Task<Result<WarehouseProductsResponse>> GetProductsInWarehouse(Guid warehouseId,int pageSize,int pageIndex);
        Task<Result<string>> UploadProductImage(Guid productId, IFormFile file);
        Task<Result<PaginatedProductsDto>> GetProductBySku(string sku, int pageNumber = 1, int pageSize = 20);
        Task<Result<dynamic>> CalculateNewStockDetails(TransactionType transactionType, int PreviousQuantity, int QuantityChanged, Warehouse FromWarehouse, Warehouse ToWarehouse);
        Task<Result<List<ProductsDto>>> GetFilteredProducts(Guid? warehouseId, Guid? supplierId, string? Name, string? Sku, string categoryName, int pageNumber = 1, int pageSize = 20);
    }
}
