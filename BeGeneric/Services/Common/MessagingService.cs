using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace BeGeneric.Services.Common
{
    public interface IMessagingService
    {
        Task ResetPasswordMessage(string emailAddress, string url);
    }

    public class MessagingService : IMessagingService
    {
        private readonly EmailSettings emailSettings;

        public MessagingService(IOptions<EmailSettings> emailSettings)
        {
            this.emailSettings = emailSettings.Value;
        }

        public async Task ResetPasswordMessage(string emailAddress, string url)
        {
            await CreateEmailAndSend(new List<string>() { emailAddress }, "Password reset", @$"Hello!

You or somebody else requested a password reset. It can be performed by clicking on <a href=""{url}"">THIS LINK</a>.
If you did not request the password reset, please ignore this email.");
        }

        private async Task CreateEmailAndSend(List<string> emails, string header, string body)
        {
            try
            {
                using var client = new SmtpClient();

                await client.ConnectAsync(emailSettings.Host, emailSettings.Port).ConfigureAwait(true);

                var credentials = new NetworkCredential
                {
                    UserName = emailSettings.Username,
                    Password = emailSettings.Password
                };

                await client.AuthenticateAsync(credentials).ConfigureAwait(true);

                var mail = new MimeKit.MimeMessage(
                    new List<string>() { emailSettings.Sender }.Select(x => new MimeKit.MailboxAddress(x, x)).ToList(),
                    emails.Select(x => new MimeKit.MailboxAddress(x, x)).ToList(), 
                    header,
                    new MimeKit.TextPart("html")
                    {
                        Text = body
                    });

                await client.SendAsync(mail).ConfigureAwait(false);

                await client.DisconnectAsync(true).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new ApplicationException("", exception);
            }
        }
    }
}
