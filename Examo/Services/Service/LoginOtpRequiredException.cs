namespace Examo.Services;

// Login par: password sahi hai, ab email par aaya OTP chahiye
public class LoginOtpRequiredException : Exception
{
    public string Email { get; }

    public LoginOtpRequiredException(string email)
        : base("We sent a 6-digit login code to your email.")
    {
        Email = email;
    }
}
