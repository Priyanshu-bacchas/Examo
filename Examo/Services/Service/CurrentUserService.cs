using System.Security.Claims;

using Examo.Services.Interfaces;

namespace Examo.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserIdOrNull
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            var value =
                user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue("nameid")
                ?? user?.FindFirstValue("sub");

            return int.TryParse(value, out var id)
                ? id
                : null;
        }
    }

    public int UserId =>
        UserIdOrNull
        ?? throw new UnauthorizedAccessException(
            "User is not authenticated.");

    public bool IsAdmin =>
        _httpContextAccessor.HttpContext?.User
            .IsInRole("Admin") ?? false;
}
