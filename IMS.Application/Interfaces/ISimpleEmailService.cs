

namespace IMS.Application.Interfaces
{
    public interface ISimpleEmailService
    {
        Task<bool> SendEmailAsync(string subject, string body);
    }
}
