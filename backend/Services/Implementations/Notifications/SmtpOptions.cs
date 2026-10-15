namespace backend.Services.Implementations.Notifications;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string SenderEmail { get; set; } = "noreply-mts@industrial.local";
    public string SenderName { get; set; } = "MTS Depo Bildirim Sistemi";
}
