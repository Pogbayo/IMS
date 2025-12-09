using System.Diagnostics;

namespace IMS.API.Middlewares.MM
{
    public class Metrics
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<Metrics> _logger;

        public Metrics(RequestDelegate next, ILogger<Metrics> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            // BEFORE REQUEST
            _logger.LogInformation("----- REQUEST START -----");
            _logger.LogInformation($"Method: {context.Request.Method}");
            _logger.LogInformation($"Path: {context.Request.Path}");
            _logger.LogInformation($"Query: {context.Request.QueryString}" ?? "No query string attached to this request");
            _logger.LogInformation($"User-Agent: {context.Request.Headers["User-Agent"]}");
            _logger.LogInformation($"Content-Length: {context.Request.ContentLength}");
            _logger.LogInformation("--------------------------");

            // Continue pipeline
            await _next(context);

            stopwatch.Stop();

            // AFTER REQUEST
            _logger.LogInformation("----- RESPONSE END -----");
            _logger.LogInformation($"Status Code: {context.Response.StatusCode}");
            _logger.LogInformation($"Response Time: {stopwatch.ElapsedMilliseconds} ms");
            _logger.LogInformation("-------------------------");
        }
    }
}
