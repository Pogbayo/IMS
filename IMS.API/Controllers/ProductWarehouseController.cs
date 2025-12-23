using IMS.Application.ApiResponse;
using IMS.Application.Interfaces;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductWarehouseController : BaseController
    {
        private readonly IProductWarehouseService _productWarehouseService;

        public ProductWarehouseController(IProductWarehouseService productWarehouseService)
        {
            _productWarehouseService = productWarehouseService;
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("update-product")]
        public async Task<IActionResult> UpdateProductInWarehouse(
            [FromQuery] Guid productId,
            [FromQuery] Guid warehouseId,
            [FromQuery] int quantityChanged,
            [FromQuery] Guid userId,
            [FromQuery] ProductUpdateInWarehouseType updateType)
        {
            try
            {
                await _productWarehouseService.UpdateProductInWarehouseAsync(
                    productId,
                    warehouseId,
                    quantityChanged,
                    userId,
                    updateType);

                return OkResponse(new { message = "Product updated successfully" });
            }
            catch (Exception ex)
            {
                return ErrorResponse("Failed to update product", ex.Message);
            }
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("transfer-product")]
        public async Task<IActionResult> TransferProduct(
            [FromQuery] Guid productId,
            [FromQuery] Guid fromWarehouseId,
            [FromQuery] Guid toWarehouseId,
            [FromQuery] int quantity,
            [FromQuery] Guid userId)
        {
            try
            {
                await _productWarehouseService.TransferProductAsync(
                    productId,
                    fromWarehouseId,
                    toWarehouseId,
                    quantity,
                    userId);

                return OkResponse(new { message = "Product transferred successfully" });
            }
            catch (Exception ex)
            {
                return ErrorResponse("Failed to transfer product", ex.Message);
            }
        }
    }
}
