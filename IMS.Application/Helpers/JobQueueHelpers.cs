using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Enums;
using IMS.Infrastructure.Mailer;
namespace IMS.Application.Helpers

{
    public static class JobQueueHelpers
    {
        public static void EnqueueAudit(this IJobQueue jobqueue, Guid userId, Guid companyId, AuditAction action, string message)
        {
            if (companyId != Guid.Empty && userId != Guid.Empty)
            {
                jobqueue.Enqueue<IAuditService>(job => job.LogAsync(
                    userId,
                    companyId,
                    action,
                    message
                ), "audit");
            }
        }

        public static void EnqueueEmail(this IJobQueue jobqueue, string email, string subject, string body)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                jobqueue.Enqueue<IMailerService>(job => job.SendEmailAsync(
                    email,
                    subject,
                    body
                ), "email");
            }
        }

        public static void EnqueueCloudWatchAudit(this IJobQueue jobqueue, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                jobqueue.Enqueue<ICloudWatchLogger>(job => job.LogAsync(
                    message
                ), "email");
            }
        }
        public static void EnqueueAWS_Ses(this IJobQueue jobqueue,List<string> emailRecipients, string subject, string body)
        {
            if (!string.IsNullOrWhiteSpace(subject) && !string.IsNullOrWhiteSpace(body))
            {
                jobqueue.Enqueue<ISimpleEmailService>(job => job.SendEmailAsync(
                    emailRecipients,
                    subject,
                    body
                ), "email");
            }
        }
    }
}
