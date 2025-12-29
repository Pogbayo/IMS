using IMS.Application.DTO.Warehouse;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WarehouseController : BaseController
    {
        private readonly IWarehouseService _warehouseService;

        public WarehouseController(IWarehouseService warehouseService)
        {
            _warehouseService = warehouseService;
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateWarehouse([FromBody] CreateWarehouseDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _warehouseService.CreateWarehouse(dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to create warehouse", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPut("update/{warehouseId}")]
        public async Task<IActionResult> UpdateWarehouse(
            [FromRoute] Guid warehouseId,
            [FromBody] UpdateWarehouseDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _warehouseService.UpdateWarehouse(warehouseId, dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to update warehouse", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("delete/{warehouseId}")]
        public async Task<IActionResult> DeleteWarehouse([FromRoute] Guid warehouseId)
        {
            var result = await _warehouseService.DeleteWarehouse(warehouseId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to delete warehouse", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPut("mark-inactive/{warehouseId}")]
        public async Task<IActionResult> MarkAsInActive([FromRoute] Guid warehouseId)
        {
            var result = await _warehouseService.MarkAsInActive(warehouseId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to mark warehouse as inactive", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPut("mark-active/{warehouseId}")]
        public async Task<IActionResult> MarkAsActive([FromRoute] Guid warehouseId)
        {
            var result = await _warehouseService.MarkAsActive(warehouseId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to mark warehouse as active", result.Message);
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetWarehouseById([FromQuery] Guid warehouseId)
        {
            var result = await _warehouseService.GetWarehouseById(warehouseId);
            return result.Success
                ? OkResponse(result)
                : NotFoundResponse(result.Error ?? "Warehouse not found", result.Message);
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-by-company")]
        public async Task<IActionResult> GetWarehouses(
            [FromQuery] Guid companyId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _warehouseService.GetWarehouses(companyId, pageNumber, pageSize);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch warehouses", result.Message);
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-by-product")]
        public async Task<IActionResult> GetWarehousesContainingProduct([FromQuery] Guid productId)
        {
            var result = await _warehouseService.GetWarehousesContainingProduct(productId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch warehouses containing product", result.Message);
        }
    }
}
//logs