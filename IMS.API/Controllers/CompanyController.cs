using IMS.Application.DTO.Company;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

    public class CompanyController : BaseController
    {
        private readonly ICompanyService _companyService;

        public CompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> RegisterCompanyAndAdmin([FromBody] CompanyCreateDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _companyService.RegisterCompanyAndAdmin(dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to register company", result.Message);
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetCompanyById([FromQuery] Guid companyId)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _companyService.GetCompanyById(companyId);
            return result.Success
                ? OkResponse(result)
                : NotFoundResponse(result.Error ?? "Company not found", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPut("update/{companyId}")]
        public async Task<IActionResult> UpdateCompany([FromRoute] Guid companyId, [FromBody] CompanyUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _companyService.UpdateCompany(companyId, dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to update company", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("delete/{companyId}")]
        public async Task<IActionResult> DeleteCompany([FromRoute] Guid companyId)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _companyService.DeleteCompany(companyId);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to delete company", result.Message);
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllCompanies([FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _companyService.GetAllCompanies(pageSize, pageNumber);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch companies", result.Message);
        }
    }
}
