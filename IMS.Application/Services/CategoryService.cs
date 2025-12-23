using IMS.Application.DTO.Category;
using IMS.Application.Interfaces;
using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IMS.Application.ApiResponse;

namespace IMS.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IAppDbContext _context;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(IAppDbContext context, ILogger<CategoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid>> CreateCategory(CreateCategoryDto dto)
        {
            try
            {
                _logger.LogInformation("Starting category creation");

                if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                    return Result<Guid>.FailureResponse("Category name is required");

                var category = new Category
                {
                    Name = dto.Name.Trim()
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Category {CategoryName} created successfully with ID {CategoryId}",
                    category.Name,
                    category.Id
                );

                return Result<Guid>.SuccessResponse(category.Id, "Category created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return Result<Guid>.FailureResponse("Error creating category", ex.Message);
            }
        }

        public async Task<Result<string>> UpdateCategory(Guid categoryId, UpdateCategoryDto dto)
        {
            try
            {
                _logger.LogInformation("Updating category {CategoryId}", categoryId);

                if (categoryId == Guid.Empty || dto == null || string.IsNullOrWhiteSpace(dto.Name))
                    return Result<string>.FailureResponse("Invalid category update data");

                var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

                if (category == null)
                    return Result<string>.FailureResponse("Category not found");

                category.Name = dto.Name.Trim();
                category.MarkAsUpdated();

                await _context.SaveChangesAsync();

                _logger.LogInformation("Category {CategoryId} updated successfully", categoryId);
                return Result<string>.SuccessResponse("Category updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category {CategoryId}", categoryId);
                return Result<string>.FailureResponse("Error updating category", ex.Message);
            }
        }

        public async Task<Result<string>> DeleteCategory(Guid categoryId)
        {
            try
            {
                _logger.LogInformation("Attempting to delete category {CategoryId}", categoryId);

                if (categoryId == Guid.Empty)
                    return Result<string>.FailureResponse("Invalid category ID");

                var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

                if (category == null)
                    return Result<string>.FailureResponse("Category not found");

                category.IsDeleted = true;
                category.MarkAsDeleted();

                await _context.SaveChangesAsync();

                _logger.LogInformation("Category {CategoryId} deleted successfully", categoryId);
                return Result<string>.SuccessResponse("Category deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category {CategoryId}", categoryId);
                return Result<string>.FailureResponse("Error deleting category", ex.Message);
            }
        }

        public async Task<Result<CategoryDto>> GetCategoryById(Guid categoryId)
        {
            try
            {
                _logger.LogInformation("Fetching category {CategoryId}", categoryId);

                if (categoryId == Guid.Empty)
                    return Result<CategoryDto>.FailureResponse("Invalid category ID");

                var category = await _context.Categories
                    .AsNoTracking()
                    .Where(c => c.Id == categoryId && !c.IsDeleted)
                    .Select(c => new CategoryDto
                    {
                        Id = c.Id,
                        Name = c.Name
                    })
                    .FirstOrDefaultAsync();

                if (category == null)
                    return Result<CategoryDto>.FailureResponse("Category not found");

                return Result<CategoryDto>.SuccessResponse(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category {CategoryId}", categoryId);
                return Result<CategoryDto>.FailureResponse("Error fetching category", ex.Message);
            }
        }

        public async Task<Result<List<CategoryDto>>> GetAllCategories()
        {
            try
            {
                _logger.LogInformation("Fetching all categories");

                var categories = await _context.Categories
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted)
                    .Select(c => new CategoryDto
                    {
                        Id = c.Id,
                        Name = c.Name
                    })
                    .ToListAsync();

                _logger.LogInformation("Fetched {Count} categories", categories.Count);
                return Result<List<CategoryDto>>.SuccessResponse(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all categories");
                return Result<List<CategoryDto>>.FailureResponse("Error fetching categories", ex.Message);
            }
        }
    }
}
