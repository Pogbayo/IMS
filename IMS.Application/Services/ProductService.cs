using IMS.Application.ApiResponse;
using IMS.Application.DTO.Product;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly ILogger<ProductService> _logger;
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUserService;
 
        public ProductService(ILogger<ProductService> logger, IAppDbContext context, IAuditService audit, ICurrentUserService currentUserService)
        {
            _logger = logger;
            _context = context;
            _audit = audit;
            _currentUserService = currentUserService;
        }

        public Task<Result<Guid>> CreateProduct(ProductCreateDto dto)
        {
            throw new NotImplementedException();
        }

        //public async Task<Result<Guid>> CreateProduct(ProductCreateDto dto)
        //{
        //    _logger.LogInformation("Creating Product");
        //    if (dto == null)
        //    {
        //        _logger.LogWarning("Bad Request");
        //        return Result<Guid>.FailureResponse("Bad Request");
        //    }

        //    var Product = new Product
        //    {
        //        Name = dto.Name,
        //        //SKU = dt
        //        ImgUrl = dto.ImgUrl,
        //        Price = dto.Price,

        //    };
        //    return Result<Guid>.SuccessResponse(Product.Id);
        //}

        public Task<Result<string>> DeleteProduct(Guid productId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ProductDto>> GetProductById(Guid productId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<List<ProductDto>>> GetProducts(Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<List<ProductDto>>> GetProductsInWarehouse(Guid warehouseId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<string>> UpdateProduct(Guid productId, ProductUpdateDto dto)
        {
            throw new NotImplementedException();
        }

        public Task<Result<string>> UploadProductImage(Guid productId, IFormFile file)
        {
            throw new NotImplementedException();
        }
    }
}
