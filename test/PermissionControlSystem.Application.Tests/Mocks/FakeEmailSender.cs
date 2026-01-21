
using System.Net.Mail;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace PermissionControlSystem.Mocks
{
    // Testlerde IEmailSender istendiğinde bu sınıf devreye girecek
    [Dependency(ReplaceServices = true)]
    public class FakeEmailSender : IEmailSender, ITransientDependency
    {
        public Task SendAsync(string to, string subject, string body, bool isBodyHtml = true)
        {
            return Task.CompletedTask;
        }

        public Task SendAsync(string from, string to, string subject, string body, bool isBodyHtml = true)
        {
            return Task.CompletedTask;
        }

        public Task QueueAsync(string to, string subject, string body, bool isBodyHtml = true)
        {
            return Task.CompletedTask;
        }

        public Task QueueAsync(string from, string to, string subject, string body, bool isBodyHtml = true)
        {
            return Task.CompletedTask;
        }

        public Task QueueAsync(string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs additionalEmailSendingArgs = null)
        {
            return Task.CompletedTask;
        }

        public Task QueueAsync(string from, string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs additionalEmailSendingArgs = null)
        {
            return Task.CompletedTask;
        }

        public Task SendAsync(string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs additionalEmailSendingArgs = null)
        {
            return Task.CompletedTask;
        }

        public Task SendAsync(string from, string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs additionalEmailSendingArgs = null)
        {
            return Task.CompletedTask;
        }

        public Task SendAsync(MailMessage mail, bool normalize = true)
        {
            return Task.CompletedTask;
        }




    }
}