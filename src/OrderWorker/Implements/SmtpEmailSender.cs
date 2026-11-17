using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using OrderWorker.Interface;

namespace OrderWorker.Implements;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _smtpSettings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpSettings smtpSettings, ILogger<SmtpEmailSender> logger)
    {
        _smtpSettings = smtpSettings;
        _logger = logger;
    }

    public async Task SendOrderConfirmationAsync(
        OrderCreatedMessage order,
        CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress("متجرنا الإلكتروني", _smtpSettings.From));
        email.To.Add(new MailboxAddress(order.CustomerName, order.CustomerEmail));
        email.Subject = OrderEmailTemplateBuilder.BuildSubject(order);

        var bodyBuilder = new BodyBuilder
        {
            TextBody = OrderEmailTemplateBuilder.BuildPlainText(order),
            HtmlBody = OrderEmailTemplateBuilder.BuildHtml(order)
        };
        email.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        var secureOption = _smtpSettings.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        _logger.LogInformation(
            "Connecting to SMTP server {Host}:{Port} to send confirmation email to {Recipient}...",
            _smtpSettings.Host,
            _smtpSettings.Port,
            order.CustomerEmail);

        await client.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, secureOption, cancellationToken);
        await client.AuthenticateAsync(_smtpSettings.User, _smtpSettings.Password, cancellationToken);
        await client.SendAsync(email, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation(
            "Email sent successfully via SMTP to {Recipient} for OrderId={OrderId}.",
            order.CustomerEmail,
            order.OrderId);
    }
}