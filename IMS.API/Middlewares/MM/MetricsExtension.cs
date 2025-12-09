namespace IMS.API.Middlewares.MM
{
    public static class MetricsExtension
    {
        public static IApplicationBuilder UseMetricsMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<Metrics>();
        }
    }
}
