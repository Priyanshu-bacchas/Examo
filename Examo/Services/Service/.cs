using System.Net;
using System.Net.Mail;

namespace Examo.Services;

public class MailService(IConfiguration cfg)
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var from = cfg["Smtp:From"] ?? cfg["Smtp:User"]!;
        using var client = new SmtpClient(cfg["Smtp:Host"], int.Parse(cfg["Smtp:Port"] ?? "587"))
        {
            Credentials = new NetworkCredential(cfg["Smtp:User"], cfg["Smtp:Password"]),
            EnableSsl = true
        };
        using var msg = new MailMessage(from, to, subject, body);
        await client.SendMailAsync(msg);
    }
}
