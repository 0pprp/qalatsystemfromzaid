using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BE_SalesEmployee.Sales.Authorization;

/// <summary>Director may read sales-manager APIs; block mutating POST/PUT/PATCH/DELETE.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DenyDirectorWritesAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsGet(method)
            && !HttpMethods.IsHead(method)
            && !HttpMethods.IsOptions(method)
            && !DirectorWriteGate.AllowsReadPost(context))
        {
            var userType = context.HttpContext.User.FindFirst("UserType")?.Value;
            if (SalesRoles.IsDirector(userType))
            {
                context.Result = new ObjectResult(new
                {
                    message = "عرض فقط — لا يمكن تنفيذ الإجراء من حساب المدير",
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
                return;
            }
        }

        await next();
    }
}

/// <summary>Shared gate used by <see cref="DenyDirectorWritesAttribute"/> (testable).</summary>
public static class DirectorWriteGate
{
    public static bool AllowsReadPost(ActionExecutingContext context)
    {
        if (context.Filters.OfType<AllowDirectorReadPostAttribute>().Any())
            return true;

        return context.ActionDescriptor.EndpointMetadata
            .OfType<AllowDirectorReadPostAttribute>()
            .Any();
    }
}
