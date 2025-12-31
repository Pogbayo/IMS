using IMS.Application.DTO.Product;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : BaseController
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        //[Authorize(Policy = "Everyone")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _productService.CreateProduct(dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to create product", result.Message);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetProductById([FromQuery] Guid productId)
        {
            var result = await _productService.GetProductById(productId);
            return result.Success
                ? OkResponse(result)
                : NotFoundResponse(result.Error ?? "Product not found", result.Message);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpGet("get-by-company")]
        public async Task<IActionResult> GetProductsByCompanyId(
            [FromQuery] Guid companyId,
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNumber = 1)
        {
            var result = await _productService.GetProductsByCompanyId(companyId, pageSize, pageNumber);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch products", result.Message);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpPut("update/{productId}")]
        public async Task<IActionResult> UpdateProduct([FromRoute] Guid productId, [FromBody] ProductUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _productService.UpdateProduct(productId, dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to update product", result.Message);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpDelete("delete/{productId}")]
        public async Task<IActionResult> DeleteProduct([FromRoute] Guid productId)
        {
            var result = await _productService.DeleteProduct(productId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to delete product", result.Message);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpGet("warehouse/{warehouseId}")]
        public async Task<IActionResult> GetProductsInWarehouse(
            [FromRoute] Guid warehouseId,
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageIndex = 1)
        {
            var result = await _productService.GetProductsInWarehouse(warehouseId, pageSize, pageIndex);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch warehouse products", result.Message);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpPost("upload-image/{productId}")]
        public async Task<IActionResult> UploadProductImage([FromRoute] Guid productId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return ErrorResponse("Invalid file");

            var result = await _productService.UploadProductImage(productId, file);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to upload image", result.Message);
        }

        //[Authorize(Policy = "Everyone")]
        [HttpGet("search-by-sku")]
        public async Task<IActionResult> GetProductBySku(
            [FromQuery] string sku,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _productService.GetProductBySku(sku, pageNumber, pageSize);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch product by SKU", result.Message);
        }

   
        //[Authorize(Policy = "Everyone")]
        [HttpGet("filter")]
        public async Task<IActionResult> GetFilteredProducts(
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? supplierId,
            [FromQuery] string? name,
            [FromQuery] string? sku,
            [FromQuery] string? categoryName,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _productService.GetFilteredProducts(
                warehouseId,
                supplierId,
                name,
                sku,
                categoryName ?? string.Empty,
                pageNumber,
                pageSize);

            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to filter products", result.Message);
        }
    }
}
