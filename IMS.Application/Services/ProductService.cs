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
using static System.Net.Mime.MediaTypeNames;


namespace IMS.Application.Services
{
    public class ProductService : IProductService
    {
    
        private readonly ILogger<ProductService> _logger;
        private readonly IAppDbContext _context;
        private readonly IJobQueue _jobqueue;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUserService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IImageService _image_service;
        private readonly IStockTransactionService _stockTransactionService;
        private readonly ICustomMemoryCache _cache;

        public ProductService(
            IJobQueue jobqueue,
            ICustomMemoryCache cache,
            IStockTransactionService stockTransactionService, 
            IImageService imageService,
            UserManager<AppUser> userManager,
            ILogger<ProductService> logger,
            IAppDbContext context,
            IAuditService audit, 
            ICurrentUserService currentUserService)
        {
            _jobqueue = jobqueue;
            _cache = cache;
            _stockTransactionService = stockTransactionService;
            _image_service = imageService;
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
        #region Helper

        private void SafeEnqueue(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job enqueue failed");
            }
        }
        #endregion

        public async Task<Result<Guid>> CreateProduct(ProductCreateDto dto)
        {
            if (dto == null)
                return Result<Guid>.FailureResponse("Bad Request");

            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            Product product;

            using (var transaction = await ((DbContext)_context).Database.BeginTransactionAsync())
            {
                try
                {
                    var supplier = await _context.Suppliers
                        .Where(s => s.Id == dto.SupplierId)
                        .Select(s => new { s.Id, s.Name })
                        .FirstOrDefaultAsync();

                    if (supplier == null)
                        return Result<Guid>.FailureResponse("Supplier not found");

                    if (dto.Warehouses == null || !dto.Warehouses.Any())
                        return Result<Guid>.FailureResponse("At least one warehouse is required");

                    var referenceWarehouseId = dto.Warehouses.First();

                    var warehouse = await _context.Warehouses
                        .Where(w => w.Id == referenceWarehouseId)
                        .Select(w => new { w.Id, w.Name })
                        .FirstOrDefaultAsync();

                    if (warehouse == null)
                        return Result<Guid>.FailureResponse("Reference warehouse not found");

                    var category = new Category { Name = dto.CategoryName! };
                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();

                    string sku = string.Empty;
                    product = null!;

                    for (int attempt = 1; attempt <= 5; attempt++)
                    {
                        var lastSku = await _context.ProductWarehouses
                            .Where(pw => pw.WarehouseId == warehouse.Id && pw.Product!.SupplierId == supplier.Id)
                            .OrderByDescending(pw => pw.Product!.SKU)
                            .Select(pw => pw.Product!.SKU)
                            .FirstOrDefaultAsync();

                        var lastNumber = lastSku == null ? 0 : SkuGenerator.GetNumericPart(lastSku);

                        sku = SkuGenerator.GenerateSku(
                            warehouse.Name,
                            dto.Name,
                            supplier.Name,
                            lastNumber + 1,
                            dto.RetailPrice
                        );

                        product = new Product
                        {
                            Name = dto.Name,
                            SKU = sku,
                            CostPrice = dto.CostPrice,
                            RetailPrice = dto.RetailPrice,
                            Profit = dto.RetailPrice - dto.CostPrice,
                            SupplierId = dto.SupplierId,
                            CompanyId = dto.CompanyId,
                            CategoryId = category.Id
                        };

                        try
                        {
                            _context.Products.Add(product);
                            await _context.SaveChangesAsync();
                            break;
                        }
                        catch (DbUpdateException ex) when
                            (ex.InnerException?.Message.Contains("IX_Products_SKU") == true)
                        {
                            if (attempt == 5)
                                throw;
                        }
                    }

                    foreach (var warehouseId in dto.Warehouses)
                    {
                        _context.ProductWarehouses.Add(new ProductWarehouse
                        {
                            ProductId = product.Id,
                            WarehouseId = warehouseId,
                            Quantity = dto.Quantity
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    if (((DbContext)_context).Database.CurrentTransaction != null)
                        await ((DbContext)_context).Database.CurrentTransaction!.RollbackAsync();


                    _logger.LogError(ex, "Error creating product");
                    return Result<Guid>.FailureResponse("An error occurred while creating the product");
                }
            }

            try
            {
                foreach (var warehouseId in dto.Warehouses)
                {
                    var stockDto = new CreateStockTransactionDto
                    {
                        ProductId = product.Id,
                        FromWarehouseId = Guid.Empty,
                        ToWarehouseId = warehouseId,
                        QuantityChanged = dto.Quantity,
                        Type = TransactionType.Purchase,
                        Note = "Initial stock added for new product",
                        CompanyId = companyId
                    };

                    await _stockTransactionService.LogTransaction(stockDto);
                    _jobqueue.EnqueueAWS_Ses(new List<string> { "adebayooluwasegun335@gmail.com" }, "Email Sent!", "Stock Transaction successfully logged");

                }

                if (dto.Image != null)
                {
                    var imageUrl = await _image_service.UploadImageAsync(dto.Image, "ims/products", product.Id);
                    product.ImgUrl = imageUrl;
                    await _context.SaveChangesAsync();
                }

                _jobqueue.EnqueueAudit(
                    userId,
                    companyId,
                    Domain.Enums.AuditAction.Create,
                    $"Product '{product.Name}' (SKU {product.SKU}) created"
                );

                _jobqueue.EnqueueCloudWatchAudit(
                    $"User {userId} created product '{product.Name}' for Company {companyId}"
                );

                foreach (var wid in dto.Warehouses)
                    _cache.RemoveByPrefix($"Warehouse:{wid}:");

                _cache.RemoveByPrefix($"Company:{companyId}:Products:");
                _cache.RemoveByPrefix("Products:");
                RemoveCompanyProductsCache(companyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Non-critical post-transaction operation failed for product {ProductId}", product.Id);
            }

            return Result<Guid>.SuccessResponse(product.Id);
        }


        public async Task<Result<ProductDto>> GetProductById(Guid productId)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            _logger.LogInformation("User {UserId} requested product with Id {ProductId}", userId, productId);

            if (productId == Guid.Empty)
            {
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, "Attempted to fetch product with empty Id"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch product with empty Id for Company {companyId}"));

                return Result<ProductDto>.FailureResponse("Bad Request: Product Id cannot be empty");
            }

            var summaryCacheKey = $"Product:{productId}:Summary";
            var stockCacheKey = $"Product:{productId}:Stock";
            var transactionsCacheKey = $"Product:{productId}:Transactions";

            if (!_cache.TryGetValue(summaryCacheKey, out ProductSummaryCache? summary))
            {
                _logger.LogInformation("Summary cache miss for product {ProductId}", productId);

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

                    SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Attempted to fetch non-existing product {productId}"));
                    SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch non-existing product {productId} for Company {companyId}"));

                    return Result<ProductDto>.FailureResponse("Product not found in the Database");
                }

                _cache.Set(summaryCacheKey, summary, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });

                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Product {summary.Name} ({productId}) loaded from DB and cached"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} loaded product {summary.Name} ({productId}) from DB and cached for Company {companyId}"));
            }
            else
            {
                _logger.LogInformation("Summary cache hit for product {ProductId}", productId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Product {productId} summary retrieved from cache"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} retrieved product {productId} summary from cache for Company {companyId}"));
            }

            if (!_cache.TryGetValue(stockCacheKey, out ProductStockDto cachedStockValue))
            {
                _logger.LogInformation("Stock cache miss for product {ProductId}", productId);

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

                _cache.Set(stockCacheKey, cachedStockValue, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Product {productId} stock loaded from DB and cached"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} loaded product {productId} stock from DB and cached for Company {companyId}"));
            }
            else
            {
                _logger.LogInformation("Stock cache hit for product {ProductId}", productId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Product {productId} stock retrieved from cache"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} retrieved product {productId} stock from cache for Company {companyId}"));
            }

            if (!_cache.TryGetValue(transactionsCacheKey, out List<StockTransactionDto> CachedTransactions))
            {
                _logger.LogInformation("Transactions cache miss for product {ProductId}", productId);

                var stockTransactions = await _context.StockTransactions
                   .Include(st => st.ProductWarehouse)
                       .ThenInclude(pw => pw!.Product)
                   .Include(st => st.FromWarehouse)
                   .Include(st => st.ToWarehouse)
                   .Where(st => st.ProductWarehouse != null && st.ProductWarehouse.ProductId == productId)
                   .OrderByDescending(st => st.TransactionDate)
                   .ToListAsync();


                CachedTransactions = new List<StockTransactionDto>();

                foreach (var st in stockTransactions)
                {
                    object? newStockData = null;

                    if (st.Type == TransactionType.Transfer)
                    {
                        newStockData = await CalculateNewStockDetails(
                            st.Type,
                            st.ProductWarehouse!.Quantity,
                            st.QuantityChanged,
                            st.FromWarehouse!,
                            st.ToWarehouse!);
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

                _cache.Set(transactionsCacheKey, CachedTransactions, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                });

                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Product {productId} transactions loaded from DB and cached"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} loaded product {productId} transactions from DB and cached for Company {companyId}"));
            }
            else
            {
                _logger.LogInformation("Transactions cache hit for product {ProductId}", productId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Product {productId} transactions retrieved from cache"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} retrieved product {productId} transactions from cache for Company {companyId}"));
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

            _logger.LogInformation("User {UserId} successfully retrieved product {ProductId}", userId, productId);
            SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Product {summary.Name} ({productId}) retrieved successfully"));
            SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} retrieved product {summary.Name} ({productId}) successfully for Company {companyId}"));

            return Result<ProductDto>.SuccessResponse(productDto, "Product retrieved successfully");
        }

        public async Task<Result<List<ProductsDto>>> GetProductsByCompanyId(Guid companyId, int pageSize, int pageNumber)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Company ID cannot be empty");
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, Guid.Empty, AuditAction.Read, "Attempted to fetch products with empty CompanyId"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch products with empty CompanyId"));
                return Result<List<ProductsDto>>.FailureResponse("Please, provide a valid company ID");
            }

            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var cacheKey = $"Company:{companyId}:Products:Page:{pageNumber}:Size:{pageSize}";

            if (_cache.TryGetValue(cacheKey, out List<ProductsDto> cachedProducts))
            {
                _logger.LogInformation("Products cache HIT for Company {CompanyId} (Page {Page}, Size {Size})", companyId, pageNumber, pageSize);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Company products (Page {pageNumber}, Size {pageSize}) retrieved from cache"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} retrieved company products (Page {pageNumber}, Size {pageSize}) from cache for Company {companyId}"));

                return Result<List<ProductsDto>>.SuccessResponse(cachedProducts, "Company products retrieved from cache");
            }

            _logger.LogInformation("Products cache MISS for Company {CompanyId} (Page {Page}, Size {Size})", companyId, pageNumber, pageSize);

            var companyExists = await _context.Companies.AnyAsync(c => c.Id == companyId);

            if (!companyExists)
            {
                _logger.LogWarning("Company {CompanyId} not found while fetching products", companyId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, "Attempted to fetch products for non-existing company"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch products for non-existing Company {companyId}"));
                return Result<List<ProductsDto>>.FailureResponse("Company with provided ID not found");
            }

            try
            {
                var companyProducts = await _context.Products
                    .Where(p => p.CompanyId == companyId)
                    .OrderByDescending(x => x.CreatedAt)
                    .OrderBy(p => p.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new ProductsDto(p.Id, p.Name, p.SKU, p.ImgUrl, p.RetailPrice))
                    .ToListAsync();

                if (!companyProducts.Any())
                {
                    _logger.LogWarning("No products found for Company {CompanyId}", companyId);
                    SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Fetched company products but none found (Page {pageNumber}, Size {pageSize})"));
                    SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} fetched products but none found for Company {companyId}"));
                    return Result<List<ProductsDto>>.FailureResponse("No products found for the company");
                }

                _cache.Set(cacheKey, companyProducts, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                });

                _logger.LogInformation("Cached {Count} products for Company {CompanyId} (Page {Page}, Size {Size})", companyProducts.Count, companyId, pageNumber, pageSize);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Fetched and cached company products (Page {pageNumber}, Size {pageSize})"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} fetched and cached company products (Page {pageNumber}, Size {pageSize}) for Company {companyId}"));

                return Result<List<ProductsDto>>.SuccessResponse(companyProducts, "Company products successfully retrieved");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error fetching products for Company {CompanyId}", companyId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Error fetching company products: {ex.Message}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"Error fetching products for Company {companyId} by User {userId}: {ex.Message}"));
                return Result<List<ProductsDto>>.FailureResponse("An unknown error occurred while fetching products");
            }
        }

        public async Task<Result<string>> UpdateProduct(Guid productId, ProductUpdateDto dto)
        {
            var userId = _currentUserService.GetCurrentUserId();

            if (productId == Guid.Empty)
            {
                _logger.LogWarning("Product ID cannot be null.");
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, Guid.Empty, AuditAction.Update, "Attempted to update product with empty ID"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to update product with empty ID"));
                return Result<string>.FailureResponse("Please provide an ID.");
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Product not found.");
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, Guid.Empty, AuditAction.Update, $"Attempted to update non-existing product {productId}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to update non-existing product {productId}"));
                return Result<string>.FailureResponse("Product with provided ID not found.");
            }

            product.Name = dto.Name ?? product.Name;
            product.RetailPrice = dto.Price ?? product.RetailPrice;
            product.ImgUrl = dto.ImgUrl ?? product.ImgUrl;

            product.MarkAsUpdated();

            await _context.SaveChangesAsync();

            SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, product.CompanyId, AuditAction.Update, $"Updated product '{product.Name}' with ID {product.Id}"));
            SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} updated product '{product.Name}' ({product.Id}) for Company {product.CompanyId}"));

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
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, Guid.Empty, AuditAction.Delete, "Attempted to delete product with empty ID"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to delete product with empty ID"));
                return Result<string>.FailureResponse("Please provide a valid ID.");
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Product not found.");
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, Guid.Empty, AuditAction.Delete, $"Attempted to delete non-existing product {productId}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to delete non-existing product {productId}"));
                return Result<string>.FailureResponse("Product with provided ID not found.");
            }

            product.MarkAsDeleted();

            await _context.SaveChangesAsync();

            SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, product.CompanyId, AuditAction.Delete, $"Deleted product '{product.Name}' with ID {product.Id}"));
            SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} deleted product '{product.Name}' ({product.Id}) for Company {product.CompanyId}"));

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
        public async Task<Result<WarehouseProductsResponse>> GetProductsInWarehouse(Guid warehouseId, int pageSize, int pageIndex)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            if (warehouseId == Guid.Empty)
            {
                _logger.LogWarning("Please, provide an ID");
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, "Attempted to fetch products with empty warehouse ID"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch products with empty warehouse ID for Company {companyId}"));
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
                    SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Attempted to fetch products with Warehouse Id: {warehouseId}"));
                    SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch products in Warehouse {warehouseId} but no products found for Company {companyId}"));
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
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Fetched products with Warehouse Id: {warehouseId}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} fetched products in Warehouse {warehouseId} successfully for Company {companyId}"));

                return Result<WarehouseProductsResponse>.SuccessResponse(response, "Warehouse products retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products in warehouse {WarehouseId} for user {UserId}", warehouseId, userId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Attempted to fetch products with Warehouse Id: {warehouseId} - Error: {ex.Message}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"Error fetching products in Warehouse {warehouseId} for User {userId}, Company {companyId}: {ex.Message}"));
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
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Update, "Attempted to upload product image with empty ProductId"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to upload product image with empty ProductId for Company {companyId}"));
                return Result<string>.FailureResponse("Invalid product ID");
            }

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Empty or null image uploaded for product {ProductId}", productId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Update, $"Attempted to upload empty image for product {productId}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to upload empty image for product {productId} for Company {companyId}"));
                return Result<string>.FailureResponse("Image file is required");
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found", productId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Update, $"Attempted to upload image for non-existing product {productId}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to upload image for non-existing product {productId} for Company {companyId}"));
                return Result<string>.FailureResponse("Product not found");
            }

            try
            {
                var imageUrl = await _image_service.UploadImageAsync(file, "ims/products", productId);

                product.ImgUrl = imageUrl;
                product.MarkAsUpdated();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Image uploaded successfully for product {ProductId}", productId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Update, $"Product image updated successfully for product {product.Name} ({productId})"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} updated product image for {product.Name} ({productId}) for Company {companyId}"));

                _cache.Remove($"Product:{productId}:Summary");
                _cache.RemoveByPrefix($"Products:Filtered:");
                _cache.RemoveByPrefix($"Products:SKU:");
                _cache.RemoveByPrefix($"Company:{companyId}:Products");

                return Result<string>.SuccessResponse(imageUrl, "Product image uploaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading image for product {ProductId}", productId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Update, $"Failed to upload product image for product {productId}. Error: {ex.Message}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"Failed to upload product image for {productId} by User {userId} for Company {companyId}: {ex.Message}"));
                return Result<string>.FailureResponse("Failed to upload product image");
            }
        }

        public async Task<Result<PaginatedProductsDto>> GetProductBySku(string sku, int pageNumber = 1, int pageSize = 20)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            if (string.IsNullOrWhiteSpace(sku))
            {
                _logger.LogWarning("User {UserId} tried to fetch products with empty SKU", userId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, "Attempted to fetch products with empty SKU"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} attempted to fetch products with empty SKU for Company {companyId}"));
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
                    SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"No products found with SKU {sku}"));
                    SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} searched for SKU {sku} but no products found for Company {companyId}"));
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
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Fetched {products.Count} products by SKU {sku}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} fetched {products.Count} products by SKU {sku} for Company {companyId}"));

                return Result<PaginatedProductsDto>.SuccessResponse(paginatedResult, "Products retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products by SKU {SKU} for user {UserId}", sku, userId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Error fetching products by SKU {sku}: {ex.Message}"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"Error fetching products by SKU {sku} for User {userId} in Company {companyId}: {ex.Message}"));
                return Result<PaginatedProductsDto>.FailureResponse("An error occurred while fetching products");
            }
        }

        public async Task<Result<List<ProductsDto>>> GetFilteredProducts(
            Guid? warehouseId, Guid? supplierId, string? Name, string? Sku, string categoryName, int pageNumber = 1, int pageSize = 20)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var companyId = await GetCurrentUserCompanyIdAsync();

            _logger.LogInformation("Products filtering started by user {UserId}", userId);

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
                _logger.LogInformation("Filtered products cache HIT for user {UserId}", userId);
                return Result<List<ProductsDto>>.SuccessResponse(cachedProducts, "Products retrieved from cache");
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
                _logger.LogWarning("No product found for the filter query by user {UserId}", userId);
                SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, "No products found with applied filters"));
                SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} applied filters but no products found for Company {companyId}"));
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

            _cache.Set(cacheKey, products, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            _logger.LogInformation("Filtered products cached with key {CacheKey} for user {UserId}", cacheKey, userId);
            SafeEnqueue(() => _jobqueue.EnqueueAudit(userId, companyId, AuditAction.Read, $"Fetched {products.Count} filtered products"));
            SafeEnqueue(() => _jobqueue.EnqueueCloudWatchAudit($"User {userId} fetched {products.Count} filtered products for Company {companyId}"));

            return Result<List<ProductsDto>>.SuccessResponse(products, "Products retrieved successfully");
        }
    }
}
