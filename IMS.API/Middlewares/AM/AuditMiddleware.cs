using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Enums;
using System.Security.Claims;

namespace IMS.API.Middlewares.AM
{
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAuditService _auditService;

        public AuditMiddleware(RequestDelegate next, IAuditService auditService)
        {
            _next = next;
            _auditService = auditService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api/auth/login") &&
                context.Request.Method == "POST")
            {
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var companyId = context.User.FindFirst("CompanyId")?.Value;
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
                var ipAddress = context.Connection.RemoteIpAddress?.ToString();

                if (Guid.TryParse(userId, out var userGuid) &&
                    Guid.TryParse(companyId, out var companyGuid))
                {
                    await _auditService.LogAsync(
                        companyGuid,
                        userGuid,
                        AuditAction.Login,  
                        $"User '{email}' logged in from IP: {ipAddress}"
                    );
                }
            }
        }

    }
}
