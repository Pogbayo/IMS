namespace IMS.Domain.Entities;
public class Expense : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}
