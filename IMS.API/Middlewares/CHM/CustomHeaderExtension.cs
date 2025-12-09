namespace IMS.API.Middlewares.CHM
{
    public static class CustomHeaderExtension
    {
        public static IApplicationBuilder UseCustomHeaderBuilder(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CustomHeader>();
        }
    }
}
