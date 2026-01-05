namespace IMS.Application.Interfaces
{
    public interface ISimpleEmailService
    {
        Task<bool> SendEmailAsync(List<string> emailRecipients,string subject, string body);
    }
}
