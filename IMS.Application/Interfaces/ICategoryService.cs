

using IMS.Application.DTO.Category;

namespace IMS.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<Guid> CreateCategory(CreateCategoryDto dto);
        Task UpdateCategory(Guid categoryId, UpdateCategoryDto dto);
        Task DeleteCategory(Guid categoryId);
        Task<CategoryDto> GetCategoryById(Guid categoryId);
        Task<List<CategoryDto>> GetCategories(Guid companyId);
    }

}
