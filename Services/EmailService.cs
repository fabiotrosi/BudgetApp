using BudgetApp.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BudgetApp.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string? _user;
        private readonly string? _password;
        private readonly string _from;
        private readonly bool _enableSsl;
        private readonly IRazorViewToStringRenderer _viewRenderer;

        public EmailService(IConfiguration config, IRazorViewToStringRenderer viewRenderer)
        {
            _host =
                config["Smtp:Host"]
                ?? throw new InvalidOperationException("Smtp:Host not configured.");
            _port = int.Parse(config["Smtp:Port"] ?? "25");
            _user = config["Smtp:User"];
            _password = config["Smtp:Password"];
            _from =
                config["Smtp:From"]
                ?? throw new InvalidOperationException("Smtp:From not configured.");
            _enableSsl = bool.Parse(config["Smtp:EnableSsl"] ?? "false");
            _viewRenderer = viewRenderer;
        }

        public async Task SendConfirmationEmailAsync(
            string toEmail,
            string displayName,
            string confirmUrl
        )
        {
            var subject = "Bitte bestätigen Sie Ihre E-Mail-Adresse";
            var model = new ConfirmationEmailViewModel
            {
                DisplayName = displayName,
                ConfirmUrl = confirmUrl,
            };
            var html = await _viewRenderer.RenderAsync("~/Views/Emails/Confirmation.cshtml", model);
            await SendAsync(toEmail, subject, html);
        }

        public async Task SendBudgetInviteEmailAsync(
            string toEmail,
            string displayName,
            string budgetName,
            string inviteUrl
        )
        {
            var subject = "Einladung zu einem Lager";
            var model = new BudgetInviteEmailViewModel
            {
                DisplayName = displayName,
                BudgetName = budgetName,
                InviteUrl = inviteUrl,
            };
            var html = await _viewRenderer.RenderAsync("~/Views/Emails/BudgetInvite.cshtml", model);
            await SendAsync(toEmail, subject, html);
        }

        private async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_from));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = _enableSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.None;
            await client.ConnectAsync(_host, _port, secureOption);
            if (!string.IsNullOrEmpty(_user))
                await client.AuthenticateAsync(_user, _password ?? string.Empty);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
