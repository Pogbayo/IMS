namespace IMS.Application.Helpers
{
    public static class SkuGenerator
    {

        public static int GetNumericPart(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                throw new ArgumentException("SKU cannot be null or empty", nameof(sku));

            var numericPart = sku.Split('-').Last();

            if (!int.TryParse(numericPart, out int number))
                throw new FormatException($"SKU '{sku}' has an invalid numeric part.");

            return number;
        }

        public static string GenerateSku(string warehouseName, string productName, string supplierName, int uniqueNumber)
        {
            if (string.IsNullOrWhiteSpace(warehouseName)) throw new ArgumentException("Warehouse name is required");
            if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name is required");
            if (string.IsNullOrWhiteSpace(supplierName)) throw new ArgumentException("Supplier name is required");

            string Clean(string input) => new string(input
                .Where(char.IsLetterOrDigit)
                .Take(3)
                .ToArray())
                .ToUpper();

            var warehouseCode = Clean(warehouseName);
            var productCode = Clean(productName);
            var supplierCode = Clean(supplierName);

            var serial = uniqueNumber.ToString("D5");

            return $"{warehouseCode}-{productCode}-{supplierCode}-{serial}";
        }
    }
}
