using IMS.Application.DTO.Product;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Application.Services;
using IMS.Domain.Enums;
using IMS.Infrastructure.Mailer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/test/notifications")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class NotificationTestController : BaseController
    {
        private readonly IJobQueue _jobQueue;
        private readonly IAuditService _auditService;
        private readonly IMailerService _mailerService;
        //private readonly IProductService _productService;

        public NotificationTestController(
            IJobQueue jobQueue,
            IAuditService auditService,
            IMailerService mailerService
            //IProductService productService
            )
        {
            _jobQueue = jobQueue;
            _auditService = auditService;
            //_productService = productService;
            _mailerService = mailerService;
        }

        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok("pong");
        }

        //[Authorize(Policy = "Everyone")]
        //[AllowAnonymous]
        //[HttpPost("create")]
        //public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        //{
        //    if (!ModelState.IsValid)
        //        return ErrorResponse("Invalid request data");

        //    var result = await _productService.CreateProduct(dto);
        //    return result.Success
        //        ? OkResponse(result)
        //        : ErrorResponse(result.Error ?? "Failed to create product", result.Message);
        //}
    }
}
