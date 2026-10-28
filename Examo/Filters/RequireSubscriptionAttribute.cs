using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Examo.Filters;

// Controller / action par lagao: free trial khatam aur plan nahi to 402 deta hai.
// Admin aur login ke bina (anonymous) requests ko nahi rokta.
public class RequireSubscriptionAttribute : TypeFilterAttribute
{
    public RequireSubscriptionAttribute()
        : base(typeof(SubscriptionAccessFilter))
    {
    }
}

public class SubscriptionAccessFilter : IAsyncActionFilter
{
    private readonly ICurrentUserService _currentUser;
    private readonly ISubscriptionService _subscriptions;

    public SubscriptionAccessFilter(
        ICurrentUserService currentUser,
        ISubscriptionService subscriptions)
    {
        _currentUser = currentUser;
        _subscriptions = subscriptions;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var userId = _currentUser.UserIdOrNull;

        if (userId == null || _currentUser.IsAdmin)
        {
            await next();
            return;
        }

        var status = await _subscriptions.GetStatusAsync(userId.Value);

        if (!status.HasAccess)
        {
            context.Result = new ObjectResult(new
            {
                message =
                    "Your free trial has ended. Please subscribe to continue.",
                code = "SUBSCRIPTION_REQUIRED"
            })
            {
                StatusCode = 402
            };

            return;
        }

        await next();
    }
}
