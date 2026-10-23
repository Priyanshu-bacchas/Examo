namespace Examo.Services.Interfaces;

public interface IEmailService
{
    // purpose: "verify" (signup) ya "reset" (forgot password)
    Task SendOtpAsync(
        string toEmail,
        string name,
        string otp,
        string purpose);

    // Admin ka reply (Help & Support) student ko email se bhejta hai
    Task SendSupportReplyAsync(
        string toEmail,
        string name,
        string subject,
        string originalMessage,
        string reply);

    // Notification (exam form last date, exam reminder, result) email se bhejta hai
    Task SendNotificationEmailAsync(
        string toEmail,
        string name,
        string title,
        string message);
}