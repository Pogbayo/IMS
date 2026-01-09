using IMS.Application.DTO.Product;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

    public class TestController : BaseController
    {
        private readonly IProductService _productService;

        public TestController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok("pong");
        }

        [Authorize(Policy = "Everyone")]
        [AllowAnonymous]
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
    }
}