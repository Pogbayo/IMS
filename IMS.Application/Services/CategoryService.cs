using IMS.Application.DTO.Category;
using IMS.Application.Interfaces;
using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

        public async Task<Guid> CreateCategory(CreateCategoryDto dto)
        {
            _logger.LogInformation("Starting category creation");

            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            {
                _logger.LogWarning("Invalid category data provided");
                throw new ArgumentException("Category name is required");
            }

            var category = new Category
            {
                Name = dto.Name.Trim(),
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Category {CategoryName} created successfully with ID {CategoryId}",
                category.Name,
                category.Id
            );

            return category.Id;
        }

        public async Task DeleteCategory(Guid categoryId)
        {
            _logger.LogInformation("Attempting to delete category {CategoryId}", categoryId);

            if (categoryId == Guid.Empty)
            {
                _logger.LogWarning("Invalid category ID provided");
                throw new ArgumentException("Invalid category ID");
            }

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

            if (category == null)
            {
                _logger.LogWarning("Category {CategoryId} not found", categoryId);
                throw new KeyNotFoundException("Category not found");
            }

            category.IsDeleted = true;
            category.MarkAsUpdated();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} deleted successfully", categoryId);
        }

        //public async Task<List<CategoryDto>> GetCategories(Guid companyId)
        //{
        //    _logger.LogInformation("Fetching categories for company {CompanyId}", companyId);

        //    var categories = await _context.Categories
        //        .AsNoTracking()
        //        .Where(c => c.CompanyId == companyId)
        //        .Select(c => new CategoryDto
        //        {
        //            Id = c.Id,
        //            Name = c.Name
        //        })
        //        .ToListAsync();

        //    _logger.LogInformation(
        //        "Fetched {Count} categories for company {CompanyId}",
        //        categories.Count,
        //        companyId
        //    );

        //    return categories;
        //}

        public async Task<CategoryDto> GetCategoryById(Guid categoryId)
        {
            _logger.LogInformation("Fetching category {CategoryId}", categoryId);

            if (categoryId == Guid.Empty)
            {
                _logger.LogWarning("Invalid category ID provided");
                throw new ArgumentException("Invalid category ID");
            }

            var category = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == categoryId)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name
                })
                .FirstOrDefaultAsync();

            if (category == null)
            {
                _logger.LogWarning("Category {CategoryId} not found", categoryId);
                throw new KeyNotFoundException("Category not found");
            }

            return category;
        }

        public async Task UpdateCategory(Guid categoryId, UpdateCategoryDto dto)
        {
            _logger.LogInformation("Updating category {CategoryId}", categoryId);

            if (categoryId == Guid.Empty || dto == null || string.IsNullOrWhiteSpace(dto.Name))
            {
                _logger.LogWarning("Invalid update data for category {CategoryId}", categoryId);
                throw new ArgumentException("Invalid category update data");
            }

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

            if (category == null)
            {
                _logger.LogWarning("Category {CategoryId} not found", categoryId);
                throw new KeyNotFoundException("Category not found");
            }

            category.Name = dto.Name.Trim();
            category.MarkAsUpdated();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} updated successfully", categoryId);
        }
    }
}
