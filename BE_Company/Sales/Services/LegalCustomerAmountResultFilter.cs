using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BE_Company.Sales.Services;

/// <summary>
/// Hides القانونية الواصل/الباقي on every JSON response except المحاسب الرئيسي.
/// </summary>
public sealed class LegalCustomerAmountResultFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var userType = context.HttpContext.Items["UserType"] as string
            ?? context.HttpContext.User.FindFirst("UserType")?.Value;
        // Legal viewers (المدير / محاسب القانونية / مدير القانونية) work with these balances.
        if (!LegalCustomerAmountRedactor.IsMainAccountant(userType)
            && !BE_Company.Legal.LegalVisibility.IsLegalViewer(context.HttpContext)
            && context.Result is ObjectResult result
            && result.StatusCode is null or >= 200 and < 300)
        {
            LegalCustomerAmountRedactor.Redact(result.Value);
        }

        return next();
    }
}
