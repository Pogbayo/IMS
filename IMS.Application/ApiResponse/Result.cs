namespace IMS.Application.ApiResponse
{
    public class Result<T>
    {
        public bool Success { get; private set; }
        public string? Message { get; private set; }
        public T? Data { get; private set; }
        public string? Error { get; private set; }

        public static Result<T> SuccessResponse(T data, string? message = null)
          => new Result<T> { Success = true, Data = data, Message = message };
        public static Result<T> FailureResponse(string error, string? message = null)
         => new Result<T> { Success = false, Error = error, Message = message };
    }
}