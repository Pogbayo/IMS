using IMS.Application.ApiResponse;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API
{
    public class BaseController : ControllerBase
    {
        protected ActionResult OkResponse<T>(T data, string? message = null)
        {
            var response = Result<T>.SuccessResponse(data, message);
            return Ok(response);
        }
        protected ActionResult ErrorResponse(string error, string? message = null)
        {
            var response = Result<object>.FailureResponse(error, message);
            return BadRequest(response);
        }

        protected ActionResult NotFoundResponse(string error = "Resource not found", string? message = null)
        {
            var response = Result<object>.FailureResponse(error, message);
            return NotFound(response);
        }

        protected ActionResult UnauthorizedResponse(string error = "Unauthorized", string? message = null)
        {
            var response = Result<object>.FailureResponse(error, message);
            return Unauthorized(response);
        }
    }
}
