using IMS.Application.ApiResponse;
using IMS.Application.DTO.StockTransaction;
using IMS.Application.Interfaces;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockTransactionController : BaseController
    {
        private readonly IStockTransactionService _stockTransactionService;

        public StockTransactionController(IStockTransactionService stockTransactionService)
        {
            _stockTransactionService = stockTransactionService;
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-transactions")]
        public async Task<IActionResult> GetStockTransactions(
            [FromQuery] Guid companyId,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] Guid? productId = null,
            [FromQuery] Guid? fromWarehouseId = null,
            [FromQuery] Guid? toWarehouseId = null,
            [FromQuery] TransactionType? transactionType = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _stockTransactionService.GetStockTransactions(
                companyId,
                fromDate,
                toDate,
                productId,
                fromWarehouseId,
                toWarehouseId,
                transactionType,
                pageNumber,
                pageSize);

            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to fetch stock transactions", result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("log-transaction")]
        public async Task<IActionResult> LogTransaction([FromBody] CreateStockTransactionDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("Invalid request data");

            var result = await _stockTransactionService.LogTransaction(dto);

            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Failed to log transaction", result.Message);
        }
    }
}
