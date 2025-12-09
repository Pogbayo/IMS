using IMS.Application.DTO.Company;
using IMS.Application.DTO.Product;
using IMS.Domain.Entities;
using IMS.Domain.Enums;

namespace IMS.Application.Helpers
{
    public sealed class CompanyCalculations
    {
            public static decimal CalculateTotalInventoryValue(IQueryable<Warehouse> warehouses)
            {

                decimal totalInventoryValue = 0;
                foreach (var warehouse in warehouses)
                {
                    foreach (var item in warehouse.ProductWarehouses)
                    {
                        totalInventoryValue += item.Quantity * item.Product!.Price;
                    }
                }
                return totalInventoryValue;
            }

            public static decimal CalculateTotalPurchases(IQueryable<StockTransaction> stockTransactions)
            {
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                return stockTransactions
                    .Where(st => st.Type == TransactionType.Purchase && st.TransactionDate >= startOfMonth)
                    .Sum(st => st.QuantityChanged * st.ProductWarehouse!.Product!.Price);
            }

            public static decimal CalculateTotalSalesTrend(IQueryable<StockTransaction> stockTransactions)
            {
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                return stockTransactions
                    .Where(st => st.Type == TransactionType.Sale && st.TransactionDate >= startOfMonth)
                    .Sum(st => st.QuantityChanged * st.ProductWarehouse!.Product!.Price);
            }

            public static int TotalSalesPerMonth(IQueryable<StockTransaction> transactions)
            {
                int month = DateTime.UtcNow.Month;
                int year = DateTime.UtcNow.Year;

                return transactions
                    .Where(st => st.Type == TransactionType.Sale
                                 && st.TransactionDate.Month == month
                                 && st.TransactionDate.Year == year)
                    .Sum(st => st.QuantityChanged);
            }


            public static IList<TopProductDto> TopProductBySales(
                 IQueryable<StockTransaction> transactions
             )
             {
               int month = DateTime.UtcNow.Month;
               int year = DateTime.UtcNow.Year;
               int top = 5;

                return transactions
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
                    .ToList();
             }
        public static List<LowOnStockProduct> GetLowOnStockProducts(
            IQueryable<ProductWarehouse> productWarehouses,
            int threshold = 10
         )
        {
            return productWarehouses
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
                .ToList();
        }

    }
}
