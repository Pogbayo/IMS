

namespace IMS.Application.Services
{
    public class ProductService
    {
        private static string GenerateSku(string name,string supplierName)
        {
            return $"{name.ToUpper().Replace(" ", "-")}-{new Random().Next(1000, 9999)}";
        }
    }
}
