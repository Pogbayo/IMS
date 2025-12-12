using Hangfire;
using IMS.Application.ApiResponse;
using IMS.Application.DTO.Product;
using IMS.Application.DTO.StockTransaction;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using System.ComponentModel.Design;
using System.Linq.Expressions;
using System.Threading.Tasks;
namespace IMS.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly ILogger<ProductService> _logger;
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUserService;
        private readonly UserManager<AppUser> _userManager;
        public ProductService(UserManager<AppUser> userManager,ILogger<ProductService> logger, IAppDbContext context, IAuditService audit, ICurrentUserService currentUserService)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
            _audit = audit;
            _currentUserService = currentUserService;
        }

        private async Task<Guid> GetCurrentUserCompanyIdAsync()
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await _userManager.Users
                .Where(u => u.Id == userId)
                .Select(u => u.CompanyId)
                .FirstOrDefaultAsync() ?? Guid.Empty;

            //if (!companyId.HasValue)
            //    _logger.LogWarning("User {UserId} does not belong to any company", userId);

            return companyId;
        }


        public async Task<Result<Guid>> CreateProduct(ProductCreateDto dto)
        {
            _logger.LogInformation("Starting product creation");

            if (dto == null)
            {
                _logger.LogWarning("Product DTO is null");
                var userIdNullDto = _currentUserService.GetCurrentUserId();
                var companyIdNullDto = await GetCurrentUserCompanyIdAsync();
                await _audit.LogAsync(userIdNullDto, companyIdNullDto, Domain.Enums.AuditAction.Create, "Attempted to create product with null DTO");
                return Result<Guid>.FailureResponse("Bad Request");
            }

            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            var supplierDeets = await _context.Suppliers
                .Where(s => s.Id == dto.SupplierId)
                .Select(sn => new { Name = sn.Name, Id = sn.Id })
                .FirstOrDefaultAsync();

            if (supplierDeets == null)
            {
                _logger.LogWarning("Supplier not found with ID {SupplierId}", dto.SupplierId);
                await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Create, $"Attempted to create product but supplier {dto.SupplierId} not found");
                return Result<Guid>.FailureResponse("Supplier not found");
            }

            // Selecting the first warehouse ID in the list asthe reference warehouse for SKU
            var referenceWarehouseId = dto.Warehouses.FirstOrDefault();
            if (referenceWarehouseId == Guid.Empty)
            {
                _logger.LogWarning("No warehouse provided for product creation");
                await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Create, "Attempted to create product without any warehouses");
                return Result<Guid>.FailureResponse("At least one warehouse is required");
            }

            var warehouseDeets = await _context.Warehouses
                .Where(w => w.Id == referenceWarehouseId)
                .Select(wn => new { Name = wn.Name, Id = wn.Id })
                .FirstOrDefaultAsync();

            if (warehouseDeets == null)
            {
                _logger.LogWarning("Reference warehouse not found with ID {WarehouseId}", referenceWarehouseId);
                await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Create, $"Attempted to create product but reference warehouse {referenceWarehouseId} not found");
                return Result<Guid>.FailureResponse("Reference warehouse not found");
            }

            var lastProductSku = await _context.ProductWarehouses
                .Where(pw => pw.WarehouseId == warehouseDeets.Id && pw.Product!.SupplierId == supplierDeets.Id)
                .OrderByDescending(pw => pw.Product!.SKU)
                .Select(p => p.Product!.SKU)
                .FirstOrDefaultAsync();

            var lastNumber = SkuGenerator.GetNumericPart(lastProductSku!);
            int uniqueNumber = lastNumber + 1;
            var ProductSku = SkuGenerator.GenerateSku(warehouseDeets.Name, dto.Name, supplierDeets.Name, uniqueNumber);

            _logger.LogInformation("Generated SKU {SKU} for product {ProductName}", ProductSku, dto.Name);

            var category = new Category { Name = dto!.CategoryName! };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created category {CategoryName}", category.Name);

            var Product = new Product
            {
                Name = dto!.Name,
                SKU = ProductSku,
                ImgUrl = dto.ImgUrl ?? "",
                RetailPrice = dto.RetailPrice,
                CategoryId = category.Id,
                SupplierId = dto.SupplierId,
                CompanyId = dto.CompanyId,
            };

            _context.Products.Add(Product);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Create, $"Product '{Product.Name}' with SKU '{Product.SKU}' created successfully");
            _logger.LogInformation("Created product {ProductName} with SKU {SKU}", Product.Name, Product.SKU);

            foreach (var warehouseId in dto.Warehouses)
            {
                var pw = new ProductWarehouse
                {
                    ProductId = Product.Id,
                    WarehouseId = warehouseId,
                    Quantity = dto.Quantity
                };

                _context.ProductWarehouses.Add(pw);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Linked product {ProductName} to warehouse {WarehouseId}", Product.Name, warehouseId);
            }

            await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Create, $"Product '{Product.Name}' linked to {dto.Warehouses.Count} warehouse(s)");

            _logger.LogInformation("Successfully created product {ProductName} and linked to {WarehouseCount} warehouse(s)", Product.Name, dto.Warehouses.Count);

            return Result<Guid>.SuccessResponse(Product.Id);
        }

        private async Task<Result<dynamic>> CalculateNewStockDetails(TransactionType transactionType, int PreviousQuantity, int QuantityChanged, Warehouse FromWarehouse, Warehouse ToWarehouse)
        {
            var previousQuantityCountForFromWarehouse = await _context.ProductWarehouses
                .Where(pw => pw.WarehouseId == FromWarehouse.Id)
                .Select(w => w.Quantity)
                .FirstOrDefaultAsync();

            var previousQuantityCountForToWarehouse = await _context.ProductWarehouses
                .Where(pw => pw.WarehouseId == ToWarehouse.Id)
                .Select(w => w.Quantity)
                .FirstOrDefaultAsync();

            if (transactionType == TransactionType.Transfer)
            {
                var TransferResult = new
                {
                    FromWarehouseName = FromWarehouse.Name,
                    ToWarehouseName = ToWarehouse.Name,
                    FromWarehousePreviousQuantity = previousQuantityCountForFromWarehouse,
                    FromWarehouseNewQuantity = previousQuantityCountForFromWarehouse - QuantityChanged,
                    ToWarehousePreviousQuantity = previousQuantityCountForToWarehouse,
                    ToWarehouseNewQuantity = previousQuantityCountForToWarehouse + QuantityChanged,
                    TransactionType = TransactionType.Transfer,
                    QuantityChanged
                };

                //return Result<dynamic>.SuccessResponse(TransferResult.Cast<dynamic>());
                return Result<dynamic>.SuccessResponse(TransferResult, "TransactionType was purchase so a dynamic result was generated for this method");
            }

            var newQuantity = transactionType switch
            {
                TransactionType.Sale => PreviousQuantity - QuantityChanged,
                TransactionType.Purchase => PreviousQuantity + QuantityChanged,
                _ => PreviousQuantity
            };

            return Result<dynamic>.SuccessResponse(new { NewQuantity = newQuantity, TransactionType = transactionType, QuantityChanged }, $"Transaction type was {transactionType}");
        }

        public async Task<Result<ProductDto>> GetProductById(Guid productId)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            _logger.LogInformation("User {UserId} requested product with Id {ProductId}", userId, productId);

            if (productId == Guid.Empty)
            {
                _logger.LogWarning("User {UserId} attempted to fetch product with empty Id", userId);
                await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Read, "Attempted to fetch product with empty Id");
                return Result<ProductDto>.FailureResponse("Bad Request: Product Id cannot be empty");
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
            {
                _logger.LogWarning("Product with Id {ProductId} not found", productId);
                await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Read, $"Attempted to fetch non-existing product with Id {productId}");
                return Result<ProductDto>.FailureResponse("Product not found");
            }

            _logger.LogInformation("Product {ProductName} (Id {ProductId}) found", product.Name, product.Id);

            var warehouseProductQuantity = await _context.ProductWarehouses
                .Where(pw => pw.ProductId == productId)
                .Select(pw => new WarehouseCount
                {
                    WarehouseName = pw.Warehouse!.Name,
                    Quantity = pw.Quantity
                })
                .ToListAsync();

            _logger.LogInformation("Fetched {WarehouseCount} warehouse quantities for product {ProductId}", warehouseProductQuantity.Count, productId);

            var overAllCount = await _context.ProductWarehouses
                .Where(pw => pw.ProductId == productId)
                .SumAsync(pw => pw.Quantity);

            ProductStockLevel stockLevel = overAllCount switch
            {
                0 => ProductStockLevel.Low,
                <= 15 => ProductStockLevel.Medium,
                <= 30 => ProductStockLevel.High,
                _ => ProductStockLevel.OutOfStock
            };

            var pw = await _context.ProductWarehouses
                .Where(pw => pw.ProductId == productId)
                .ToListAsync();

            _logger.LogInformation("Calculated overall count {OverAllCount} and stock level {StockLevel} for product {ProductId}", overAllCount, stockLevel, productId);

            var supplierInfo = await _context.Suppliers
                .Where(s => s.Id == product.SupplierId)
                .Select(s => new SupplierInfo
                {
                    SupplierName = s.Name,
                    PhoneNumber = s.PhoneNumber,
                    Email = s.Email
                })
                .FirstOrDefaultAsync() ?? new SupplierInfo { Email = "", PhoneNumber = "", SupplierName = "" };

            _logger.LogInformation("Fetched supplier info for product {ProductId}", productId);

            var stockTransactions = await _context.StockTransactions
                .Where(st => st.ProductWarehouse!.ProductId == productId)
                .Include(st => st.ProductWarehouse) 
                .ToListAsync();


            //This first approach wasn't feasible because I needed a method that was awaited to calculate new stock level but couldn't be inluded in an EF core Select method
            //var productTransactions = await _context.StockTransactions
            //    .Where(st => st.ProductWarehouse!.ProductId == productId)
            //    .Select(st => new StockTransactionDto
            //    {
            //        QuantityChanged = st.QuantityChanged,
            //        Type = st.Type,
            //        Note = st.Note,
            //        TransactionDate = st.TransactionDate,
            //        WarehouseId = st.ProductWarehouse!.WarehouseId,
            //        NewStockLevel =
            //    })
            //    .ToListAsync();
                  

            var productTransactions = new List<StockTransactionDto>();

            foreach (var st in stockTransactions)
            {
                object? newStockData;

                if (st.Type == TransactionType.Transfer)
                {
                    var result = await CalculateNewStockDetails(st.Type,
                    st.ProductWarehouse!.Quantity,
                    st.QuantityChanged,
                    st.FromWarehouse!,
                    st.ToWarehouse!);

                    newStockData = result;
                }
                else if (st.Type == TransactionType.Purchase)
                {
                    newStockData = new PurchaseStockLevelDto
                    {
                        PreviousQuantity = st.ProductWarehouse!.Quantity,
                        NewQuantity = st.ProductWarehouse.Quantity + st.QuantityChanged
                    };
                }
                else if (st.Type == TransactionType.Sale)
                {
                    newStockData = new SaleStockLevelDto
                    {
                        PreviousQuantity = st.ProductWarehouse!.Quantity,
                        NewQuantity = st.ProductWarehouse.Quantity - st.QuantityChanged
                    };
                }
                else
                {
                    newStockData = null;
                }

                var productTransaction = new StockTransactionDto
                {
                    QuantityChanged = st.QuantityChanged,
                    Type = st.Type,
                    Note = st.Note,
                    TransactionDate = st.TransactionDate,
                    WarehouseId = st.ProductWarehouse!.WarehouseId,
                    WarehouseName = st.FromWarehouse!.Name,
                    NewStockLevel = newStockData
                };

                productTransactions.Add(productTransaction);
            }

            _logger.LogInformation("Fetched {TransactionCount} stock transactions for product {ProductId}", productTransactions.Count, productId);

            var productDto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                ImgUrl = product.ImgUrl,
                RetailPrice = product.RetailPrice,
                Quantity = warehouseProductQuantity,
                OverAllCount = overAllCount,
                StockLevel = stockLevel,
                SupplierInfo = supplierInfo,
                Transactions = productTransactions
            };

            _logger.LogInformation("User {UserId} successfully retrieved product {ProductId}", userId, productId);
            await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Read, $"Product {product.Name} (Id {productId}) retrieved successfully by user {userId}");

            return Result<ProductDto>.SuccessResponse(productDto, "Product retrieved successfully");
        }

        public async Task<Result<List<dynamic>>> GetProductsByCompanyId(Guid companyId)
        {
            var userId = _currentUserService.GetCurrentUserId();
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Company ID can not be null");
                return Result<List<dynamic>>.FailureResponse("Please, provide an ID");
            }

            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
            {
                _logger.LogWarning("Company with provided ID not found");
                return Result<List<dynamic>>.FailureResponse("Company with provideed ID not found");
            }

            try
            {
                var companyProducts = await _context.Products
                    .Where(p => p.CompanyId == companyId)
                    .Select(p => new { Id = p.Id, Name = p.Name, SKU = p.SKU, ImgUrl = p.ImgUrl, RetailPrice = p.RetailPrice })
                    .ToListAsync();

                if (!companyProducts.Any())
                {
                    _logger.LogWarning("No products found for the company");
                    return Result<List<dynamic>>.FailureResponse("No products found for the company");
                }

                _logger.LogInformation("Products retrieved successfully");
                BackgroundJob.Enqueue<IAuditService>("audit", job => job.LogAsync(userId, companyId, AuditAction.Read, $"Company products fetched successfully by {userId}"));

                return Result<List<dynamic>>.SuccessResponse(companyProducts.Cast<dynamic>().ToList(), "Company products successfully retrieved");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogCritical($"An unknown error occurred while fetching company products: {ex.Message}");
                return Result<List<dynamic>>.FailureResponse("An unknown error occurred while fetching products for company");
            }
        }

        public async Task<Result<string>> UpdateProduct(Guid productId, ProductUpdateDto dto)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (productId == Guid.Empty)
            {
                _logger.LogWarning("Product ID cannot be null.");
                return Result<string>.FailureResponse("Please provide an ID.");
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Product not found.");
                return Result<string>.FailureResponse("Product with provided ID not found.");
            }

            product.Name = dto.Name ?? product.Name;
            product.RetailPrice = dto.Price ?? product.RetailPrice;
            product.ImgUrl = dto.ImgUrl ?? product.ImgUrl;

            product.MarkAsUpdated();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                userId,
                product.CompanyId, 
                AuditAction.Update,
                $"Updated product '{product.Name}' with ID {product.Id}"
            );

            return Result<string>.SuccessResponse("Product updated successfully.");
        }


        public async Task<Result<string>> DeleteProduct(Guid productId)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (productId == Guid.Empty)
            {
                _logger.LogWarning("Product ID cannot be null.");
                return Result<string>.FailureResponse("Please provide a valid ID.");
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Product not found.");
                return Result<string>.FailureResponse("Product with provided ID not found.");
            }

            product.MarkAsDeleted();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                userId,
                product.CompanyId, 
                AuditAction.Delete,
                $"Deleted product '{product.Name}' with ID {product.Id}"
            );

            _logger.LogInformation("Product '{ProductName}' marked as deleted by user {UserId}", product.Name, userId);

            return Result<string>.SuccessResponse("Product deleted successfully.");
        }


        public Task<Result<List<ProductDto>>> GetProductsInWarehouse(Guid warehouseId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<string>> UploadProductImage(Guid productId, IFormFile file)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ProductDto>> GetProductBySku(string sku)
        {
            throw new NotImplementedException();
        }

        public Task<Result<List<ProductDto>>> GetFilteredProducts(Guid? warehouseId = null, Guid? supplierId = null, int pageNumber = 1, int pageSize = 20)
        {
            throw new NotImplementedException();
        }
    }
}
