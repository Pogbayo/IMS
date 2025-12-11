using IMS.Application.DTO.Company;
using IMS.Application.DTO.Product;
using IMS.Application.Interfaces;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Helpers
{
    public sealed class CompanyCalculations : ICompanyCalculations
    {
            public async Task<decimal> CalculateTotalInventoryValue(IQueryable<Warehouse> warehouses)
            {

                return await warehouses
                       .SelectMany(w => w.ProductWarehouses)
                       .SumAsync(pw => pw.Quantity * pw.Product!.Price);
            }

            public async Task<decimal> CalculateTotalPurchases(IQueryable<StockTransaction> stockTransactions)
            {
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                return await stockTransactions
                    .Where(st => st.Type == TransactionType.Purchase && st.TransactionDate >= startOfMonth)
                    .SumAsync(st => st.QuantityChanged * st.ProductWarehouse!.Product!.Price);
            }
            
            public async Task<decimal> CalculateTotalSalesTrend(IQueryable<StockTransaction> stockTransactions)
            {
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                return await stockTransactions
                    .Where(st => st.Type == TransactionType.Sale && st.TransactionDate >= startOfMonth)
                    .SumAsync(st => st.QuantityChanged * st.ProductWarehouse!.Product!.Price);
            }

            public async  Task<int> TotalSalesPerMonth(IQueryable<StockTransaction> transactions)
            {
                int month = DateTime.UtcNow.Month;
                int year = DateTime.UtcNow.Year;

                return await transactions
                    .Where(st => st.Type == TransactionType.Sale
                                 && st.TransactionDate.Month == month
                                 && st.TransactionDate.Year == year)
                    .SumAsync(st => st.QuantityChanged);
            }

            public async Task<IList<TopProductDto>> TopProductBySales(
                 IQueryable<StockTransaction> transactions
             )
             {
               int month = DateTime.UtcNow.Month;
               int year = DateTime.UtcNow.Year;
               int top = 5;

                return await  transactions
                    .Where(st => st.Type == TransactionType.Sale
                                 && st.TransactionDate.Month == month
                                 && st.TransactionDate.Year == year)
                    .GroupBy(st => st.ProductWarehouse!.Product!)
                    .Select(g => new TopProductDto
                    {
                        ProductId = g.Key.Id,
                        Name = g.Key.Name,
                        QuantitySold = g.Sum(x => x.QuantityChanged),
                        TotalRevenue = g.Sum(x => x.QuantityChanged * g.Key.Price)
                    })
                    .OrderByDescending(x => x.TotalRevenue)
                    .Take(top)
                    .ToListAsync();
             }

            public async Task<List<LowOnStockProduct>> GetLowOnStockProducts(
                IQueryable<ProductWarehouse> productWarehouses,
                int threshold = 10
             )
            {
                return await productWarehouses
                    .GroupBy(pw => pw.Product!)
                    .Select(g => new
                    {
                        Product = g.Key,
                        TotalQuantity = g.Sum(pw => pw.Quantity)
                    })
                    .Where(x => x.TotalQuantity < threshold)
                    .Select(x => new LowOnStockProduct
                    {
                        Name = x.Product.Name,
                        Price = x.Product.Price
                    })
                    .ToListAsync();
            }

    }
}
