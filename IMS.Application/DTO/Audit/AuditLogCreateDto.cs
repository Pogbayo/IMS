namespace IMS.Application.DTO.Audit
{
    public class AuditLogCreateDto
    {
        public Guid userId { get; set; }
        public Guid companyId { get; set; }
        public required string Action { get; set; }
        public string? Details { get; set; }
    }
}
