using Hangfire;
using IMS.Application.ApiResponse;
using IMS.Application.DTO.Product;
using IMS.Application.DTO.StockTransaction;
using IMS.Application.DTO.Warehouse;
using IMS.Application.Helpers;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly ILogger<ProductService> _logger;
        private readonly IAppDbContext _context;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUserService;
        private readonly UserManager<AppUser> _userManager;
        public readonly IImageService _imageService;
        public ProductService(IImageService imageService,UserManager<AppUser> userManager,ILogger<ProductService> logger, IAppDbContext context, IAuditService audit, ICurrentUserService currentUserService)
        {
            _imageService = imageService;
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

            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync();

            try
            {
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

                // Selecting the first warehouse ID in the list as the reference warehouse for SKU
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

                string productSku = string.Empty;
                bool SkuSaved = false;
                int SkuAttempt = 0;

                while (!SkuSaved && SkuAttempt < 5)
                {
                    SkuAttempt++;

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
                        ImgUrl = "",
                        RetailPrice = dto.RetailPrice,
                        CostPrice = dto.CostPrice,
                        Profit = dto.RetailPrice - dto.CostPrice,
                        CategoryId = category.Id,
                        SupplierId = dto.SupplierId,
                        CompanyId = dto.CompanyId,
                    };

                    await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Create, $"Product '{Product.Name}' with SKU '{Product.SKU}' created successfully");
                    _logger.LogInformation("Created product {ProductName} with SKU {SKU}", Product.Name, Product.SKU);

                    try
                    {
                        _context.Products.Add(Product);
                        await _context.SaveChangesAsync();

                        var productImageUrl = await _imageService.UploadImageAsync(dto.Image!, "ims/products",Product.Id);

                        Product.ImgUrl = productImageUrl;

                        SkuSaved = true;

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
                            await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Create, $"Product '{Product.Name}' linked to {dto.Warehouses.Count} warehouse(s)");
                            _logger.LogInformation("Successfully created product {ProductName} and linked to {WarehouseCount} warehouse(s)", Product.Name, dto.Warehouses.Count);

                            await transaction.CommitAsync();
                            return Result<Guid>.SuccessResponse(Product.Id);
                        }
                    }
                    catch (DbUpdateException db)
                    {

                        if (db.InnerException?.Message.Contains("IX_Products_SKU") == true)
                        {
                            _logger.LogWarning("SKU conflict detected, retrying... Attempt {Attempt}", SkuAttempt);
                           ((DbContext)_context).Entry(Product).State = EntityState.Detached;
                            continue; 
                        }

                        throw;
                    }
                }
            return Result<Guid>.FailureResponse("Failed to create product after multiple attempts due to SKU conflicts");
            }
            catch (Exception ex)
            {

                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating product");
                return Result<Guid>.FailureResponse("An error occurred while creating the product"); ;
            }
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
                CostPrice = product.CostPrice,
                //Profit = product.SetProfit(),
                Profit = product.RetailPrice - product.CostPrice,
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

        public async Task<Result<List<dynamic>>> GetProductsByCompanyId(Guid companyId, int pageSize, int pageNumber)
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

            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            try
            {
                var companyProducts = await _context.Products
                    .Where(p => p.CompanyId == companyId)
                    .Skip(pageNumber * pageSize)
                    .Take(pageSize)
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

            BackgroundJob.Enqueue<IAuditService>("audit", job => job.LogAsync(userId,
                product.CompanyId,
                AuditAction.Delete,
                $"Deleted product '{product.Name}' with ID {product.Id}"));
           
            _logger.LogInformation("Product '{ProductName}' marked as deleted by user {UserId}", product.Name, userId);

            return Result<string>.SuccessResponse("Product deleted successfully.");
        }

        public async Task<Result<WarehouseProductsResponse>> GetProductsInWarehouse(Guid warehouseId, int pageSize, int pageIndex)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            if (warehouseId == Guid.Empty)
            {
                _logger.LogWarning("Please, provide an ID");
                return Result<WarehouseProductsResponse>.FailureResponse("Please, provide an ID");
            }

            try
            {
                var query = _context.ProductWarehouses
               .Where(pw => pw.WarehouseId == warehouseId);

                var totalCount = await query.CountAsync();

                var WarehouseProducts = await query
                    .AsNoTracking()
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .Select(pw => new ProductsDto
                    {
                        Id = pw.ProductId,
                        Name = pw.Product!.Name,
                        SKU = pw.Product.SKU,
                        ImgUrl = pw.Product.ImgUrl,
                        RetailPrice = pw.Product.RetailPrice,
                    }).ToListAsync();

                if (WarehouseProducts.Count <= 0 && !WarehouseProducts.Any())
                {
                    _logger.LogInformation($"User{userId} attempted to fetch products in warehouse: {warehouseId}");
                    await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Read, $"Attempted to fetch products with Warehouse Id: {warehouseId}");
                    return Result<WarehouseProductsResponse>.FailureResponse("Error occured: Query returned empty list");
                }

                var warehouseProducts = new WarehouseProductsResponse
                {
                    OverAllCount = totalCount,
                    Products = WarehouseProducts
                };

                _logger.LogInformation($"User{userId} fetched products in warehouse: {warehouseId}");
                await _audit.LogAsync(userId, companyId,AuditAction.Read, $"Fetched products with Warehouse Id: {warehouseId}");
                return Result<WarehouseProductsResponse>.SuccessResponse(warehouseProducts, "Warehouse products retrieved successfully");
            }
            catch (Exception ex)
            {

                _logger.LogInformation($"User{userId} attempted to fetch products in warehouse: {warehouseId}",ex);
                await _audit.LogAsync(userId, companyId, Domain.Enums.AuditAction.Read, $"Attempted to fetch products with Warehouse Id: {warehouseId},");
                return Result<WarehouseProductsResponse>.FailureResponse("Error occured: Query returned empty list"); ;
            }
        }

        public async Task<Result<string>> UploadProductImage(Guid productId, IFormFile file)
        {
            _logger.LogInformation("Starting image upload for product {ProductId}", productId);

            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            if (productId == Guid.Empty)
            {
                _logger.LogWarning("Invalid ProductId provided");

                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Update,
                    "Attempted to upload product image with empty ProductId"
                );

                return Result<string>.FailureResponse("Invalid product ID");
            }

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Empty or null image uploaded for product {ProductId}", productId);

                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Update,
                    $"Attempted to upload empty image for product {productId}"
                );

                return Result<string>.FailureResponse("Image file is required");
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found", productId);

                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Update,
                    $"Attempted to upload image for non-existing product {productId}"
                );

                return Result<string>.FailureResponse("Product not found");
            }

            try
            {
                var imageUrl = await _imageService.UploadImageAsync(
                    file,
                    "ims/products",
                    productId
                );

                product.ImgUrl = imageUrl;
                product.MarkAsUpdated();

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Image uploaded successfully for product {ProductId}",
                    productId
                );

                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Update,
                    $"Product image updated successfully for product {product.Name} ({productId})"
                );

                return Result<string>.SuccessResponse(imageUrl, "Product image uploaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while uploading image for product {ProductId}",
                    productId
                );

                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Update,
                    $"Failed to upload product image for product {productId}. Error: {ex.Message}"
                );

                return Result<string>.FailureResponse("Failed to upload product image");
            }
        }

        public async Task<Result<PaginatedProductsDto>> GetProductBySku(
            string sku,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            if (string.IsNullOrWhiteSpace(sku))
            {
                _logger.LogWarning("User {UserId} tried to fetch products with empty SKU", userId);
                return Result<PaginatedProductsDto>.FailureResponse("Please provide a valid SKU to search with");
            }

            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 100); 

            try
            {
                var totalCount = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.SKU.Contains(sku))
                    .CountAsync();

                if (totalCount == 0)
                {
                    _logger.LogInformation("No products found with SKU {SKU} for user {UserId}", sku, userId);
                    await _audit.LogAsync(userId, companyId, AuditAction.Read, $"No products found with SKU {sku}");
                    return Result<PaginatedProductsDto>.FailureResponse("No products found with this SKU");
                }

                var products = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.SKU.Contains(sku))
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new ProductsDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        SKU = p.SKU,
                        ImgUrl = p.ImgUrl,
                        RetailPrice = p.RetailPrice
                    })
                    .ToListAsync();

                var PaginatedResult = new PaginatedProductsDto
                {
                    Products = products,
                    TotalCount = totalCount
                };

                _logger.LogInformation("User {UserId} fetched {Count} products by SKU {SKU}", userId, products.Count, sku);
                await _audit.LogAsync(userId, companyId, AuditAction.Read, $"Fetched {products.Count} products by SKU {sku}");

                return Result<PaginatedProductsDto>.SuccessResponse(PaginatedResult, "Products retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products by SKU {SKU} for user {UserId}", sku, userId);
                return Result<PaginatedProductsDto>.FailureResponse("An error occurred while fetching products");
            }
        }

        public async Task<Result<List<ProductsDto>>> GetFilteredProducts(Guid? warehouseId , Guid? supplierId , string? Name , string? Sku, string categoryName , int pageNumber = 1, int pageSize = 20)
        {
            _logger.LogInformation("Products filtering...");

            var query = _context.Products.AsQueryable();

            if (warehouseId.HasValue)
                query = query.Where(p => p.ProductWarehouses.Any(pw => pw.WarehouseId == warehouseId.Value));

            if (supplierId.HasValue)
                query = query.Where(p => p.SupplierId == supplierId.Value);

            if (!string.IsNullOrWhiteSpace(Name))
                query = query.Where(p => p.Name.Contains(Name));

            if (!string.IsNullOrWhiteSpace(Sku))
                query = query.Where(p => p.SKU.Contains(Sku));

            if (!string.IsNullOrWhiteSpace(categoryName))
                query = query.Where(p => p.Category.Name.Contains(categoryName));

            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductsDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    ImgUrl = p.ImgUrl,
                    RetailPrice = p.RetailPrice
                })
                .ToListAsync();

            if (totalCount == 0)
            {
                _logger.LogWarning("No product with filter query found");
                return Result<List<ProductsDto>>.FailureResponse("No products found");
            }
            return Result<List<ProductsDto>>.SuccessResponse(products, "Products retrieved successfully.");
        }
    }
}
