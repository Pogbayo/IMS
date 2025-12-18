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
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IImageService _imageService;
        private readonly IStockTransactionService _stockTransaction;
        private readonly ICustomMemoryCache _cache;
        public ProductService(ICustomMemoryCache cache,IStockTransactionService stockTransaction,IImageService imageService,UserManager<AppUser> userManager,ILogger<ProductService> logger, IAppDbContext context, IAuditService audit, ICurrentUserService currentUserService)
        {
            _cache = cache;
            _stockTransaction = stockTransaction;
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

        private void RemoveCompanyProductsCache(Guid companyId)
        {
            var prefix = $"Company:{companyId}:Products:";

            foreach (var key in _cache.GetKeys().Where(k => k.StartsWith(prefix)))
            {
                _cache.Remove(key);
            }
        }

        public async Task<Result<dynamic>> CalculateNewStockDetails(TransactionType transactionType, int PreviousQuantity, int QuantityChanged, Warehouse FromWarehouse, Warehouse ToWarehouse)
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

                // Selecting the first warehouse ID in the list as the referecne warehouse for SKU
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
                            var transactionDto = new CreateStockTransactionDto
                            {
                                ProductId = Product.Id,
                                QuantityChanged = pw.Quantity,
                                Type = TransactionType.Purchase,
                                CompanyId = Product.CompanyId,
                                Note = $"{userId} added this product to warehouse {pw.Warehouse!.Name} on {Product.CreatedAt}",
                                FromWarehouseId = pw.WarehouseId,
                                ToWarehouseId = null 
                            };

                            await _stockTransaction.LogTransaction
                                (transactionDto);

                            foreach (var item in dto.Warehouses)
                            {
                                _cache.RemoveByPrefix($"Warehouse:{item}:");
                            }

                            _cache.RemoveByPrefix($"Company:{companyId}:Products:");
                            _cache.RemoveByPrefix($"Products:Filtered:");
                            _cache.RemoveByPrefix($"Products:SKU:");

                            RemoveCompanyProductsCache(Product.CompanyId);
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
                await _audit.LogAsync(
                    userId,
                    companyId,
                    AuditAction.Read,
                    "Attempted to fetch product with empty Id");

                return Result<ProductDto>
                    .FailureResponse("Bad Request: Product Id cannot be empty");
            }

            var summaryCacheKey = $"Product:{productId}:Summary";

            var stockCacheKey = $"Product:{productId}:Stock";

            var transactionsCacheKey = $"Product:{productId}:Transactions";


            if (!_cache.TryGetValue(summaryCacheKey, out ProductSummaryCache? summary))
            {
                _logger.LogInformation(
                  "Summary cache miss for product {ProductId}",
                  productId);

                summary = await _context.Products
                     .Where(p => p.Id == productId)
                     .Select(p => new ProductSummaryCache
                     {
                         Id = p.Id,
                         Name = p.Name,
                         SKU = p.SKU,
                         ImgUrl = p.ImgUrl!,
                         RetailPrice = p.RetailPrice,
                         CostPrice = p.CostPrice,
                         Profit = p.RetailPrice - p.CostPrice,
                         SupplierInfo = new SupplierInfo
                         {
                             Id = p.Supplier.Id,
                             SupplierName = p.Supplier.Name,
                             PhoneNumber = p.Supplier.PhoneNumber,
                             Email = p.Supplier.Email
                         }
                     }).FirstOrDefaultAsync();

                if (summary == null)
                {
                    _logger.LogWarning("Product not found");

                    await _audit.LogAsync(
                       userId,
                       companyId,
                       AuditAction.Read,
                       $"Attempted to fetch non-existing product {productId}");

                    return Result<ProductDto>.FailureResponse("Product not found in the Database");
                }

                _cache.Set(summaryCacheKey, summary, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });
            }
            else
            {
                _logger.LogInformation(
                    "Summary cache hit for product {ProductId}",
                    productId);
            }


            if (!_cache.TryGetValue(stockCacheKey, out ProductStockDto cachedStockValue))
            {
                _logger.LogInformation(
                    "Stock cache miss for product {ProductId}",
                    productId);

                var warehouses = await _context.ProductWarehouses
                    .Where(pw => pw.ProductId == productId)
                    .Include(pw => pw.Warehouse)
                    .ToListAsync();

                var overAllCount = warehouses.Sum(w => w.Quantity);

                cachedStockValue = new ProductStockDto
                {
                    Warehouses = warehouses.Select(w => new WarehouseCount
                    {
                        WarehouseName = w.Warehouse!.Name,
                        Quantity = w.Quantity
                    }).ToList(),

                    OverAllCount = overAllCount,

                    StockLevel = overAllCount switch
                    {
                        0 => ProductStockLevel.Low,
                        <= 15 => ProductStockLevel.Medium,
                        <= 30 => ProductStockLevel.High,
                        _ => ProductStockLevel.OutOfStock
                    }
                };

                _cache.Set(stockCacheKey, cachedStockValue,
                   new MemoryCacheEntryOptions
                   {
                       AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                   });
            }
            else
            {
                _logger.LogInformation(
                    "Stock cache hit for product {ProductId}",
                    productId);
            }


            if (!_cache.TryGetValue(transactionsCacheKey, out List<StockTransactionDto> CachedTransactions))
            {
                _logger.LogInformation(
                   "Transactions cache miss for product {ProductId}",
                   productId);

                var stockTransactions = await _context.StockTransactions
                    .Where(st => st.ProductWarehouse!.ProductId == productId)
                    .Include(st => st.ProductWarehouse)
                    .ToListAsync();

                CachedTransactions = new List<StockTransactionDto>();

                foreach (var st in stockTransactions)
                {
                    object? newStockData = null;

                    if (st.Type == TransactionType.Transfer)
                    {
                        var result = await CalculateNewStockDetails(
                        st.Type,
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

                    CachedTransactions.Add(new StockTransactionDto
                    {
                        QuantityChanged = st.QuantityChanged,
                        Type = st.Type,
                        Note = st.Note,
                        TransactionDate = st.TransactionDate,
                        WarehouseId = st.ProductWarehouse!.WarehouseId,
                        WarehouseName = st.FromWarehouse!.Name,
                        NewStockLevel = newStockData
                    });
                }

                _cache.Set(
                      transactionsCacheKey,
                      CachedTransactions,
                      new MemoryCacheEntryOptions
                      {
                          AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                      });

                _logger.LogInformation("Fetched {TransactionCount} stock transactions for product {ProductId}", cachedStockValue.OverAllCount, productId);
            }
            else
            {
                _logger.LogInformation(
                    "Transactions cache hit for product {ProductId}",
                    productId);
            }

            var productDto = new ProductDto
            {
                Id = summary!.Id,
                Name = summary.Name,
                SKU = summary.SKU,
                ImgUrl = summary.ImgUrl,
                RetailPrice = summary.RetailPrice,
                CostPrice = summary.CostPrice,
                Profit = summary.RetailPrice - summary.CostPrice,
                Quantity = cachedStockValue.Warehouses,
                OverAllCount = cachedStockValue.OverAllCount,
                StockLevel = cachedStockValue.StockLevel,
                SupplierInfo = summary.SupplierInfo,
                Transactions = CachedTransactions
            };

            _logger.LogInformation(
                   "User {UserId} successfully retrieved product {ProductId}",
                   userId, productId);

            await _audit.LogAsync(
                userId,
                companyId,
                AuditAction.Read,
                $"Product {summary.Name} ({productId}) retrieved successfully");

            return Result<ProductDto>
                .SuccessResponse(productDto, "Product retrieved successfully");
        }

        public async Task<Result<List<ProductsDto>>> GetProductsByCompanyId(Guid companyId,int pageSize,int pageNumber)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Company ID cannot be empty");
                return Result<List<ProductsDto>>
                    .FailureResponse("Please, provide a valid company ID");
            }

            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var cacheKey =
                $"Company:{companyId}:Products:Page:{pageNumber}:Size:{pageSize}";

   
            if (_cache.TryGetValue(cacheKey, out List<ProductsDto> cachedProducts))
            {
                _logger.LogInformation(
                    "Products cache HIT for Company {CompanyId} (Page {Page}, Size {Size})",
                    companyId, pageNumber, pageSize);

                return Result<List<ProductsDto>>
                    .SuccessResponse(cachedProducts, "Company products retrieved from cache");
            }

            _logger.LogInformation(
                "Products cache MISS for Company {CompanyId} (Page {Page}, Size {Size})",
                companyId, pageNumber, pageSize);

      
            var companyExists = await _context.Companies
                .AnyAsync(c => c.Id == companyId);

            if (!companyExists)
            {
                _logger.LogWarning(
                    "Company {CompanyId} not found while fetching products",
                    companyId);

                return Result<List<ProductsDto>>
                    .FailureResponse("Company with provided ID not found");
            }

            try
            {
                var companyProducts = await _context.Products
                    .Where(p => p.CompanyId == companyId)
                    .OrderBy(p => p.Name)
                    .Skip((pageNumber - 1) * pageSize) 
                    .Take(pageSize)
                    .Select(p => new ProductsDto(
                        p.Id,
                        p.Name,
                        p.SKU,
                        p.ImgUrl,
                        p.RetailPrice
                    ))
                    .ToListAsync();

                if (!companyProducts.Any())
                {
                    _logger.LogWarning(
                        "No products found for Company {CompanyId}",
                        companyId);

                    return Result<List<ProductsDto>>
                        .FailureResponse("No products found for the company");
                }

        
                _cache.Set(
                    cacheKey,
                    companyProducts,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });

                _logger.LogInformation(
                    "Cached {Count} products for Company {CompanyId} (Page {Page}, Size {Size})",
                    companyProducts.Count, companyId, pageNumber, pageSize);

               
                BackgroundJob.Enqueue<IAuditService>(
                    "audit",
                    job => job.LogAsync(
                        userId,
                        companyId,
                        AuditAction.Read,
                        $"Fetched company products (Page {pageNumber}, Size {pageSize})"));

                return Result<List<ProductsDto>>
                    .SuccessResponse(companyProducts, "Company products successfully retrieved");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    "Error fetching products for Company {CompanyId}",
                    companyId);

                return Result<List<ProductsDto>>
                    .FailureResponse("An unknown error occurred while fetching products");
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

            _cache.Remove($"Product:{productId}:Summary");
            _cache.Remove($"Product:{productId}:Stock");
            _cache.Remove($"Product:{productId}:Transactions");
            _cache.Remove($"Company:{product.CompanyId}:Products");

            _cache.RemoveByPrefix($"Products:Filtered:");      
            _cache.RemoveByPrefix($"Products:SKU:");           

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
            _cache.Remove($"Product:{productId}:Summary");
            _cache.Remove($"Product:{productId}:Stock");
            _cache.Remove($"Product:{productId}:Transactions");

            _cache.RemoveByPrefix($"Products:Filtered:");
            _cache.RemoveByPrefix($"Products:SKU:");
            _cache.RemoveByPrefix($"Company:{product.CompanyId}:Products");
            _cache.RemoveByPrefix($"Warehouse:");  

            return Result<string>.SuccessResponse("Product deleted successfully.");
        }

        public async Task<Result<WarehouseProductsResponse>> GetProductsInWarehouse(Guid warehouseId,int pageSize,int pageIndex)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            if (warehouseId == Guid.Empty)
            {
                _logger.LogWarning("Please, provide an ID");
                return Result<WarehouseProductsResponse>.FailureResponse("Please, provide an ID");
            }

            var cacheKey = $"Warehouse:{warehouseId}:Page:{pageIndex}:Size:{pageSize}";

            if (_cache.TryGetValue(cacheKey, out WarehouseProductsResponse? cachedResult))
            {
                _logger.LogInformation("Cache HIT for warehouse {WarehouseId}, page {PageIndex}", warehouseId, pageIndex);
                return Result<WarehouseProductsResponse>.SuccessResponse(cachedResult!, "Warehouse products retrieved from cache");
            }

            try
            {
                var query = _context.ProductWarehouses
                    .Where(pw => pw.WarehouseId == warehouseId);

                var totalCount = await query.CountAsync();

                var warehouseProductsList = await query
                    .AsNoTracking()
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .Select(pw => new ProductsDto(
                        pw.ProductId,
                        pw.Product!.Name,
                        pw.Product.SKU,
                        pw.Product.ImgUrl,
                        pw.Product.RetailPrice
                    ))
                    .ToListAsync();

                if (!warehouseProductsList.Any())
                {
                    _logger.LogInformation("User {UserId} attempted to fetch products in warehouse {WarehouseId} but no products found", userId, warehouseId);
                    await _audit.LogAsync(userId, companyId, AuditAction.Read, $"Attempted to fetch products with Warehouse Id: {warehouseId}");
                    return Result<WarehouseProductsResponse>.FailureResponse("No products found in this warehouse");
                }

                var response = new WarehouseProductsResponse
                {
                    OverAllCount = totalCount,
                    Products = warehouseProductsList
                };

                _cache.Set(cacheKey, response, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                });

                _logger.LogInformation("User {UserId} fetched products in warehouse {WarehouseId}", userId, warehouseId);
                await _audit.LogAsync(userId, companyId, AuditAction.Read, $"Fetched products with Warehouse Id: {warehouseId}");

                return Result<WarehouseProductsResponse>.SuccessResponse(response, "Warehouse products retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products in warehouse {WarehouseId} for user {UserId}", warehouseId, userId);
                await _audit.LogAsync(userId, companyId, AuditAction.Read, $"Attempted to fetch products with Warehouse Id: {warehouseId}");
                return Result<WarehouseProductsResponse>.FailureResponse("An error occurred while fetching warehouse products");
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
                _cache.Remove($"Product:{productId}:Summary");
                _cache.RemoveByPrefix($"Products:Filtered:");  
                _cache.RemoveByPrefix($"Products:SKU:");       
                _cache.RemoveByPrefix($"Company:{companyId}:Products"); 

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

        public async Task<Result<PaginatedProductsDto>> GetProductBySku(string sku, int pageNumber = 1,int pageSize = 20)
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

           
            var cacheKey = $"Products:SKU:{sku}:Page:{pageNumber}:Size:{pageSize}";

            if (_cache.TryGetValue(cacheKey, out PaginatedProductsDto? cachedResult))
            {
                _logger.LogInformation("Cache HIT for SKU search {SKU}, page {Page}", sku, pageNumber);
                return Result<PaginatedProductsDto>.SuccessResponse(cachedResult!, "Products retrieved from cache");
            }

            try
            {
                var query = _context.Products.AsNoTracking()
                    .Where(p => p.SKU.Contains(sku));

                var totalCount = await query.CountAsync();

                if (totalCount == 0)
                {
                    _logger.LogInformation("No products found with SKU {SKU} for user {UserId}", sku, userId);
                    await _audit.LogAsync(userId, companyId, AuditAction.Read, $"No products found with SKU {sku}");
                    return Result<PaginatedProductsDto>.FailureResponse("No products found with this SKU");
                }

                var products = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new ProductsDto(
                        p.Id,
                        p.Name,
                        p.SKU,
                        p.ImgUrl,
                        p.RetailPrice
                    ))
                    .ToListAsync();

                var paginatedResult = new PaginatedProductsDto
                {
                    Products = products,
                    TotalCount = totalCount
                };

               
                _cache.Set(cacheKey, paginatedResult, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                });

                _logger.LogInformation("User {UserId} fetched {Count} products by SKU {SKU} and cached the result", userId, products.Count, sku);
                await _audit.LogAsync(userId, companyId, AuditAction.Read, $"Fetched {products.Count} products by SKU {sku}");

                return Result<PaginatedProductsDto>.SuccessResponse(paginatedResult, "Products retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products by SKU {SKU} for user {UserId}", sku, userId);
                return Result<PaginatedProductsDto>.FailureResponse("An error occurred while fetching products");
            }
        }

        public async Task<Result<List<ProductsDto>>> GetFilteredProducts(Guid? warehouseId,Guid? supplierId,string? Name,string? Sku,string categoryName,int pageNumber = 1,int pageSize = 20)
        {
            _logger.LogInformation("Products filtering...");

            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

    
            var cacheKey = $"Products:Filtered:" +
                           $"Warehouse:{warehouseId}:" +
                           $"Supplier:{supplierId}:" +
                           $"Name:{Name}:" +
                           $"Sku:{Sku}:" +
                           $"Category:{categoryName}:" +
                           $"Page:{pageNumber}:" +
                           $"Size:{pageSize}";

        
            if (_cache.TryGetValue(cacheKey, out List<ProductsDto> cachedProducts))
            {
                _logger.LogInformation("Filtered products cache HIT");
                return Result<List<ProductsDto>>.SuccessResponse(
                    cachedProducts,
                    "Products retrieved from cache");
            }

        
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

            if (totalCount == 0)
            {
                _logger.LogWarning("No product with filter query found");
                return Result<List<ProductsDto>>.FailureResponse("No products found");
            }

          
            var products = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductsDto(
                    p.Id,
                    p.Name,
                    p.SKU,
                    p.ImgUrl,
                    p.RetailPrice
                ))
                .ToListAsync();

           
            _cache.Set(
                cacheKey,
                products,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                });

            _logger.LogInformation("Filtered products cached with key {CacheKey}", cacheKey);

            return Result<List<ProductsDto>>.SuccessResponse(
                products,
                "Products retrieved successfully");
        }
    }
}
