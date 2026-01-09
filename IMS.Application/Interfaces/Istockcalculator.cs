using IMS.Domain.Entities;
using IMS.Domain.Enums;

namespace IMS.Application.Interfaces
{
    public interface IStockCalculator
    {
        object CalculateNewStockDetails(
            TransactionType type,
            int currentQuantity,
            int quantityChanged,
            Warehouse fromWarehouse,
            Warehouse toWarehouse
        );
    }

}
