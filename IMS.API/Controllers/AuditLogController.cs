using IMS.Application.Interfaces.IAudit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditService _auditService;

        public AuditLogController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        [Authorize]
        [HttpGet("who-am-i")]
        public IActionResult WhoAmI()
        {
            var claims = User.Claims.Select(c => new
            {
                c.Type,
                c.Value
            });

            return Ok(claims);
        }

        //[Authorize(Policy = "AdminOnly")]
        //[HttpGet("get-audits")]
        //public async Task<IActionResult> GetAudits(
        //    [FromQuery] Guid companyId,
        //    [FromQuery] int pageSize = 10,
        //    [FromQuery] int pageNumber = 1)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    var result = await _auditService.GetAudits(companyId, pageSize, pageNumber);
        //    return result.Success
        //        ? OkResponse(result)
        //        : ErrorResponse(result.Error!, result.Message);
        //}

        [Authorize]
        [HttpGet("with-auth")]
        public IActionResult WithAuth()
        {
            return Ok(new { message = "Auth works!", claims = User.Claims.Select(c => new { c.Type, c.Value }) });
        }

        [HttpGet("test-auth")]
        public IActionResult TestAuth()
        {
            var authHeader = Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authHeader))
                return Ok(new { Message = "No Authorization header" });

            if (!authHeader.StartsWith("Bearer "))
                return Ok(new { Message = "Authorization header doesn't start with 'Bearer '" });

            var token = authHeader.Substring("Bearer ".Length).Trim();

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                return Ok(new
                {
                    Message = "Token parsed successfully",
                    Claims = jwtToken.Claims.Select(c => new { c.Type, c.Value }),
                    Expires = jwtToken.ValidTo
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Message = "Token parse failed", Error = ex.Message });
            }
        }
    }
}
