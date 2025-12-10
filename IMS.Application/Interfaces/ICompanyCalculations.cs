using IMS.Application.DTO.Company;
using IMS.Application.DTO.Product;
using IMS.Domain.Entities;


namespace IMS.Application.Interfaces
{
    public interface ICompanyCalculations
    {
        Task<decimal> CalculateTotalInventoryValue(
            IQueryable<Warehouse> warehouses
            );
        Task<decimal>  CalculateTotalPurchases(
            IQueryable<StockTransaction> stockTransactions
            );
        Task<decimal> CalculateTotalSalesTrend(
            IQueryable<StockTransaction> stockTransactions
            );
        Task<List<LowOnStockProduct>> GetLowOnStockProducts(
             IQueryable<ProductWarehouse> productWarehouses,
             int threshold = 10
            );
        Task<IList<TopProductDto>> TopProductBySales(
              IQueryable<StockTransaction> transactions
            );
        Task<int> TotalSalesPerMonth(
            IQueryable<StockTransaction> transactions
            );
    }
}
