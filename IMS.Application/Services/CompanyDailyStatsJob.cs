using IMS.Application.Interfaces;
using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IMS.Application.Services
{
    public class CompanyDailyStatsJob : ICompanyDailyStatJob
    {
        private readonly IAppDbContext _context;
        private readonly ICompanyCalculations _companyCalculations;
        public CompanyDailyStatsJob(IAppDbContext context, ICompanyCalculations companyCalculations)
        {
            _context = context;
            _companyCalculations = companyCalculations;
        }

        public async Task RunDailyStat()
        {
            var companies =  _context.Companies.AsAsyncEnumerable();

            await foreach (var company in companies)
            {
                var productWarehouses = _context.ProductWarehouses
                     .Include(pw => pw.Product)
                     .Where(pw => pw.Product!.CompanyId == company.Id);
             
                var warehouses = _context.Warehouses
                    .Include(c => c.ProductWarehouses)
                        .ThenInclude(pw => pw.Product)
                    .Where(c => c.CompanyId == company.Id);

                var stockTransactions = _context.StockTransactions
                    .Include(st => st.ProductWarehouse)
                        .ThenInclude(pw => pw!.Product)
                    .Where(st => st.CompanyId == company.Id);

                //var totalPurchase = _companyCalculations.CalculateTotalPurchases(stockTransactions);
                //var salesTrend = _companyCalculations.CalculateTotalSalesTrend(stockTransactions);
                //var totalSalesPerMonth = _companyCalculations.TotalSalesPerMonth(stockTransactions);

                var totalInv = await _companyCalculations.CalculateTotalInventoryValue(warehouses);
                var topProducts = _companyCalculations.TopProductBySales(stockTransactions);
                var lowStock = _companyCalculations.GetLowOnStockProducts(productWarehouses);

                //serilaizing the retiurn objects from the CompanyCalculatiion methods
                var topProductsJson = JsonSerializer.Serialize(topProducts);
                var lowStockJson = JsonSerializer.Serialize(lowStock);

                var stat = new CompanyDailyStat
                {
                    CompanyId = company.Id,
                    StatDate = DateTime.UtcNow.Date,
                    TotalInventoryValue = totalInv,
                    TopProductsBySalesJson = topProductsJson,
                    LowOnStockProductsJson = lowStockJson
                };
                _context.CompanyDailyStats.Add(stat);
                await _context.SaveChangesAsync();
            }
        }
    }
}
