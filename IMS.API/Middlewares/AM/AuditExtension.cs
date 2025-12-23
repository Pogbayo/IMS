using IMS.API.Middlewares.AM;

namespace IMS.API.Middlewares.CHM
{
    public static class AuditExtension
    {
        public static IApplicationBuilder UseAuditBuilder(this IApplicationBuilder app)
        {
            return app.UseMiddleware<AuditMiddleware>();
        }
    }
}
