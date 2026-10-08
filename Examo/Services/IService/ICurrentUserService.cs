namespace Examo.Services.Interfaces;

public interface ICurrentUserService
{
    // Logged-in user ki Id (JWT se). Login nahi hai to exception.
    int UserId { get; }

    int? UserIdOrNull { get; }

    bool IsAdmin { get; }
}
