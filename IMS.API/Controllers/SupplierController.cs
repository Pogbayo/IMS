using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SupplierController : BaseController
    {
        private readonly ISupplierService _supplierService;

        public SupplierController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("register/{companyId}")]
        public async Task<IActionResult> RegisterSupplierToCompany(
            [FromRoute] Guid companyId,
            [FromBody] SupplierCreateDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _supplierService.RegisterSupplierToCompany(companyId, dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to register supplier", result.Message);
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllSuppliers()
        {
            var result = await _supplierService.GetAllSuppliers();
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch suppliers", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateSupplier([FromBody] SupplierUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _supplierService.UpdateSupplier(dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to update supplier", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("delete/{supplierId}")]
        public async Task<IActionResult> DeleteSupplier([FromRoute] Guid supplierId)
        {
            var result = await _supplierService.DeleteSupplier(supplierId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to delete supplier", result.Message);
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-by-name")]
        public async Task<IActionResult> GetSupplierByName([FromQuery] string name)
        {
            var result = await _supplierService.GetSupplierByName(name);
            return result.Success
                ? OkResponse(result)
                : NotFoundResponse(result.Error ?? "Supplier not found", result.Message);
        }
    }
}
