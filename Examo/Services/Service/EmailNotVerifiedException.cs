namespace Examo.Services;

// Login par: password sahi hai par email verify nahi hui
public class EmailNotVerifiedException : Exception
{
    public string Email { get; }

    public EmailNotVerifiedException(string email)
        : base("Please verify your email. We have sent a new code.")
    {
        Email = email;
    }
}