namespace IMS.Infrastructure.Mailer
{
    public interface IMailerService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}
