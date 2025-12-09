namespace IMS.API.Middlewares.CHM
{
    public class CustomHeader
    {
        private readonly RequestDelegate _next;

        public CustomHeader(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            Console.WriteLine($"User In");

            //Adding a customheader to the request
            context.Request.Headers.Append("X-App-Client", "CodeHub");

            //Before the request gets to the controllerin the app server
            Console.WriteLine($"Incoming request: {context.Request.Method} {context.Request.Path}");
            await _next(context);

            // AFTER controller runs
            if (!context.Response.HasStarted)
            {
                context.Response.Headers.Append("X-Powered-By", "CodeHub.API");
            }

            Console.WriteLine($"Response code: {context.Response.StatusCode}");
            Console.WriteLine($"User Out");
        }
    }
}