using System.Threading.Tasks;

namespace HRTestWeb.Services.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string html);
    }
}
