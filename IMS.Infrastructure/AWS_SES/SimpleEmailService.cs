using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using IMS.Application.Interfaces;

namespace IMS.Infrastructure.AWS_SES
{
    public class SimpleEmailService : ISimpleEmailService
    {
        private readonly IAmazonSimpleEmailService _sesClient;

        public SimpleEmailService(IAmazonSimpleEmailService sesClient)
        {
            _sesClient = sesClient;
        }

        public async Task<bool> SendEmailAsync(List<string> emailRecipients,string subject, string body)
        {

            var fromEmail = "noreply@tech-expert-beta.com.ng";
            var displayName = "InvManager";

            var formattedSource = $"{displayName} <{fromEmail}>";

            var sendRequest = new SendEmailRequest
            {
                Source = formattedSource,
                Destination = new Destination
                {
                    ToAddresses = emailRecipients
                },

                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content(body)
                    }
                }
            };

            try
            {
                var response = await _sesClient.SendEmailAsync(sendRequest);
                Console.WriteLine($"Email sent! Message ID: {response.MessageId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
                return false;
            }
        }
    }
}
