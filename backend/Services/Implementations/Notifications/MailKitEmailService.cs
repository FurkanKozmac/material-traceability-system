using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using backend.Services.Interfaces;

namespace backend.Services.Implementations.Notifications;

public sealed class MailKitEmailService(IOptions<SmtpOptions> options, IHostEnvironment environment) : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.Value.SenderName, options.Value.SenderEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        if (environment.IsDevelopment())
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(options.Value.Host, options.Value.Port, MailKit.Security.SecureSocketOptions.Auto, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
