using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;

namespace IMS.API.Middlewares.GBM
{
    public class GlobalException
    {
        private  readonly RequestDelegate _next;
        private readonly ILogger<GlobalException> _logger;

        public GlobalException(RequestDelegate next, ILogger<GlobalException> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, ex.Message);
                await WriteErrorResponse(context, HttpStatusCode.NotFound, ex.Message);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, ex.Message);

                var errors = new List<string> { ex.Message };

                await WriteErrorResponse(context, HttpStatusCode.BadRequest, "Validation failed", errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "An unexpected error occurred");
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message, object? errors = null)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message,
                errors
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
