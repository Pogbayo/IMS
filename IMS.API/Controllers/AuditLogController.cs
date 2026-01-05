using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Infrastructure.Mailer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

    public class AuditLogController : BaseController
    {
        private readonly IAuditService _auditService;
        private readonly IMailerService _mailerService;
        private readonly ISimpleEmailService _sesMailer;
        private readonly IJobQueue _jobqueue;
        public AuditLogController(IJobQueue jobqueue, IAuditService auditService, IMailerService mailerService, ISimpleEmailService sesMailer)
        {
            _jobqueue = jobqueue;
            _auditService = auditService;
            _sesMailer = sesMailer;
            _mailerService =mailerService;
        }

        [Authorize(Policy = "AdminOnly")]
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

        //[AllowAnonymous]
        //[HttpGet("test-email")]
        //public async Task<IActionResult> TestEmail()
        //{
        //    await Task.Run(() =>
        //    {
        //        _jobqueue.EnqueueEmail(
        //            "adebayooluwasegun335@gmail.com",
        //            "SMTP Test",
        //            "If you see this, SMTP works."
        //        );
        //    });

        //    return Ok("Email sent");
        //}

        //[AllowAnonymous]
        //[HttpGet("test-email-with-ses")]
        //public async Task<IActionResult> TestEmailSes()
        //{
        //    await _sesMailer.SendEmailAsync(
        //        "adebayooluwasegun335@gmail.com",
        //        "SMTP Test"
        //    );

        //    return Ok("Email sent");
        //}

    }
}
