using System.Net;
using System.Net.Mail;
using ClothingStore.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClothingStore.Infrastructure.Notifications;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Log" (default, writes the mail to the console) or "Smtp" (smtp4dev / real SMTP).</summary>
    public string Provider { get; set; } = "Log";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@clothingstore.local";
    public string FromName { get; set; } = "ClothingStore";
}

/// <summary>Development: the whole mail (with its links) is written to the log.</summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogWarning("EMAIL (not sent, Email:Provider=Log)\nTo: {To}\nSubject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var o = options.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(o.FromAddress, o.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl };
        if (!string.IsNullOrEmpty(o.UserName))
            client.Credentials = new NetworkCredential(o.UserName, o.Password);

        await client.SendMailAsync(message, ct);
    }
}
