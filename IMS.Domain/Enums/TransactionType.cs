namespace IMS.Domain.Enums
{
    public enum TransactionType : byte
    {
        Purchase = 1,       // Products coming in from a supplier
        Sale = 2,           // Products going out to another company (your client)
        Transfer = 3,       // Moving products between warehouses
        //Adjustment = 4,   // Manual correction (damaged/lost items)
        //Return = 5        // Returned products from clients
    }
}
