namespace IMS.API.Middlewares.GBM
{
    public static class GlobalExceptionExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionBuilder(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalException>();
        }
    }
}
