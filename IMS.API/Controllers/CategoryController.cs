using IMS.Application.DTO.Category;
using IMS.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CategoryController : BaseController
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.CreateCategory(dto);
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error!, result.Message);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPut("update/{categoryId}")]
        public async Task<IActionResult> UpdateCategory(
            [FromRoute] Guid categoryId,
            [FromBody] UpdateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _categoryService.UpdateCategory(categoryId, dto);
            return OkResponse("Category updated successfully");
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("delete/{categoryId}")]
        public async Task<IActionResult> DeleteCategory([FromRoute] Guid categoryId)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _categoryService.DeleteCategory(categoryId);
            return OkResponse("Category deleted successfully");
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetCategoryById([FromQuery] Guid categoryId)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.GetCategoryById(categoryId);
            return result.Success
                ? OkResponse(result)
                : NotFoundResponse(result.Error ?? "Category not found");
        }

        [Authorize(Policy = "Everyone")]
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllCategories()
        {
            var result = await _categoryService.GetAllCategories();
            return result.Success
                ? OkResponse(result)
                : ErrorResponse(result.Error ?? "Error fetching categories");
        }
    }
}
