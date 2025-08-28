using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using HRTestWeb.Services.Settings;

namespace HRTestWeb.Services.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _defaults;
        private readonly IAppSettingsService _app;

        public SmtpEmailSender(IOptions<SmtpOptions> opt, IAppSettingsService app)
        {
            _defaults = opt.Value;
            _app = app;
        }

        public async Task SendAsync(string to, string subject, string html)
        {
            var s = await _app.GetCachedAsync();

            var host = string.IsNullOrWhiteSpace(s.SmtpHost) ? _defaults.Host : s.SmtpHost;
            var port = s.SmtpPort > 0 ? s.SmtpPort : _defaults.Port;
            var user = string.IsNullOrWhiteSpace(s.SmtpUser) ? _defaults.User : s.SmtpUser;
            var pass = string.IsNullOrWhiteSpace(s.SmtpPass) ? _defaults.Pass : s.SmtpPass;
            var from = string.IsNullOrWhiteSpace(s.From) ? _defaults.From : s.From;

            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(from));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);

            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, pass);

            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
    }
}
