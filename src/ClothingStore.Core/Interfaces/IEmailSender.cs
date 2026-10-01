namespace ClothingStore.Core.Interfaces;

/// <summary>Local: Log (link shows in the console) or Smtp (smtp4dev). Live: Brevo/SendGrid.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
