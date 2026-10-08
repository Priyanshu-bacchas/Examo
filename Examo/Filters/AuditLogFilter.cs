using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Examo.Filters;

// Har successful POST / PUT / DELETE ko Logs & Security me save karta hai.
// (GET requests aur Auth controller skip hote hain; Auth khud login log karta hai.)
public class AuditLogFilter : IAsyncActionFilter
{
    public const string DetailsKey = "AuditDetails";

    // Controller chahe to poora action naam khud de sakta hai (e.g. "Block User")
    public const string ActionKey = "AuditAction";

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var executed = await next();

        try
        {
            var http = context.HttpContext;
            var method = http.Request.Method;

            if (HttpMethods.IsGet(method) ||
                HttpMethods.IsHead(method) ||
                HttpMethods.IsOptions(method))
            {
                return;
            }

            var controller =
                context.RouteData.Values["controller"]?.ToString()
                ?? "Unknown";

            if (controller == "Auth")
                return;

            if (executed.Exception != null &&
                !executed.ExceptionHandled)
            {
                return;
            }

            var status =
                (executed.Result as IStatusCodeActionResult)
                    ?.StatusCode
                ?? http.Response.StatusCode;

            if (status >= 400)
                return;

            var actionName =
                context.RouteData.Values["action"]?.ToString()
                ?? method;

            var label = actionName switch
            {
                "Create" => "Create",
                "Update" => "Update",
                "Delete" => "Delete",
                "ResetPassword" => "Reset Password",
                _ => actionName
            };

            var details =
                http.Items[DetailsKey] as string
                ?? $"{method} {http.Request.Path}";

            var logService =
                http.RequestServices
                    .GetRequiredService<IActivityLogService>();

            var actionText =
                http.Items[ActionKey] as string
                ?? $"{label} {controller}";

            await logService.LogAsync(
                actionText,
                details);
        }
        catch
        {
            // Audit fail hone se request kabhi fail nahi hogi
        }
    }
}
