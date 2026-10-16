namespace Examo.Services.Interfaces;

public interface IEmailService
{
    // purpose: "verify" (signup) ya "reset" (forgot password)
    Task SendOtpAsync(
        string toEmail,
        string name,
        string otp,
        string purpose);
}