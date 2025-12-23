using IMS.Application.DTO.Category;
using IMS.Application.ApiResponse;

namespace IMS.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<Result<Guid>> CreateCategory(CreateCategoryDto dto);
        Task<Result<string>> UpdateCategory(Guid categoryId, UpdateCategoryDto dto);
        Task<Result<string>> DeleteCategory(Guid categoryId);
        Task<Result<CategoryDto>> GetCategoryById(Guid categoryId);
        Task<Result<List<CategoryDto>>> GetAllCategories();
    }
}
