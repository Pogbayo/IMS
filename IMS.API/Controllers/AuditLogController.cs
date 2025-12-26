using IMS.Application.Interfaces.IAudit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : BaseController
    {
        private readonly IAuditService _auditService;

        public AuditController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        //[Authorize(Policy = "AdminOnly")]
        [HttpGet("get-audits")]
        public async Task<IActionResult> GetAudits(
            [FromQuery] Guid companyId,
            [FromQuery] int pageSize = 10,
            [FromQuery] int pageNumber = 1)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _auditService.GetAudits(companyId, pageSize, pageNumber);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!, result.Message);
        }
    }
}
