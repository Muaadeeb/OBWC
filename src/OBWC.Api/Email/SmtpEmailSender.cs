using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OBWC.Api.Email;

public sealed record CapturedEmail(string To, string Subject, string Body)
{
    public string? Link =>
        Body.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part =>
                part.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
}

public sealed class EmailOutbox
{
    private readonly List<CapturedEmail> _items = [];
    private readonly Lock _gate = new();

    public void Add(CapturedEmail email)
    {
        lock (_gate)
            _items.Add(email);
    }

    public IReadOnlyList<CapturedEmail> Snapshot()
    {
        lock (_gate)
            return _items.ToArray();
    }

    public CapturedEmail? LatestTo(string email)
    {
        lock (_gate)
        {
            for (var index = _items.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_items[index].To, email, StringComparison.OrdinalIgnoreCase))
                    return _items[index];
            }
        }

        return null;
    }
}

public interface IClubMailer
{
    bool IsConfigured { get; }

    Task SendAsync(string to, string subject, string textBody, string? replyToName, string? replyToAddress, CancellationToken cancellationToken);
}

public sealed class MailNotConfiguredException : Exception
{
    public MailNotConfiguredException() : base("SMTP is not configured.")
    {
    }
}

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> smtpOptions,
    IHostEnvironment environment,
    EmailOutbox outbox,
    ILogger<SmtpEmailSender> logger) : IEmailSender<ApplicationUser>, IClubMailer
{
    private readonly SmtpOptions _smtp = smtpOptions.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_smtp.Host) && !string.IsNullOrWhiteSpace(_smtp.FromAddress);

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your OBWC account",
            $"Confirm your email for the Omaha Beach Wargaming Club site:\n{confirmationLink}\n",
            null, null, CancellationToken.None);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your OBWC password",
            $"Reset your password for the Omaha Beach Wargaming Club site:\n{resetLink}\n",
            null, null, CancellationToken.None);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "OBWC password reset code", $"Password reset code: {resetCode}", null, null, CancellationToken.None);

    public async Task SendAsync(
        string to,
        string subject,
        string textBody,
        string? replyToName,
        string? replyToAddress,
        CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            outbox.Add(new CapturedEmail(to, subject, textBody));

        if (!IsConfigured)
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                var link = new CapturedEmail(to, subject, textBody).Link;
                logger.LogInformation(
                    "SMTP is not configured. Captured {Subject} for {To}. Link: {Link}{NewLine}{Body}",
                    subject,
                    to,
                    link ?? "(none)",
                    Environment.NewLine,
                    textBody);
                return;
            }

            logger.LogWarning("SMTP is not configured. Email to {To} was not sent.", to);
            throw new MailNotConfiguredException();
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromDisplayName, _smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrWhiteSpace(replyToAddress))
            message.ReplyTo.Add(new MailboxAddress(replyToName ?? replyToAddress, replyToAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = textBody };

        using var client = new SmtpClient();
        var socket = !_smtp.EnableSsl
            ? SecureSocketOptions.None
            : _smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_smtp.Host, _smtp.Port, socket, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_smtp.UserName))
            await client.AuthenticateAsync(_smtp.UserName, _smtp.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
