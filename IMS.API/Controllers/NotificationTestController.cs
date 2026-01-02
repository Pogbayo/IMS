using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
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
    public class NotificationTestController : ControllerBase
    {
        private readonly IJobQueue _jobQueue;
        private readonly IAuditService _auditService;
        private readonly IMailerService _mailerService;

        public NotificationTestController(
            IJobQueue jobQueue,
            IAuditService auditService,
            IMailerService mailerService)
        {
            _jobQueue = jobQueue;
            _auditService = auditService;
            _mailerService = mailerService;
        }

        // 🔹 1️⃣ QUEUED (Hangfire)
        [Authorize]
        [HttpPost("queued")]
        public IActionResult SendQueued(
            [FromQuery] Guid userId,
            [FromQuery] Guid companyId,
            [FromQuery] string email)
        {
            // enqueue audit
            _jobQueue.Enqueue<IAuditService>(job =>
                job.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Create,
                    $"[QUEUED] Audit logged for {email}"
                )
            );

            // enqueue email
            _jobQueue.Enqueue<IMailerService>(job =>
                job.SendEmailAsync(
                    email,
                    "Queued Email Test",
                    "<h3>This email was sent via Hangfire</h3>"
                )
            );

            return Ok("Audit + Email QUEUED successfully");
        }

        // 🔹 2️⃣ DIRECT (No Hangfire)
        [Authorize]
        [HttpPost("direct")]
        public async Task<IActionResult> SendDirect(
            [FromQuery] Guid userId,
            [FromQuery] Guid companyId,
            [FromQuery] string email)
        {
            await _auditService.LogAsync(
                userId,
                companyId,
                AuditAction.Create,
                $"[DIRECT] Audit logged for {email}"
            );

            await _mailerService.SendEmailAsync(
                email,
                "Direct Email Test",
                "<h3>This email was sent directly</h3>"
            );

            return Ok("Audit + Email SENT directly");
        }
    }
}
