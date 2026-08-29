using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.AspNetCore.Diagnostics;

public static class ExceptionMiddleware
{
    public static void UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(async context =>
        {
            var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionHandlerFeature?.Error;

            if (exception != null)
            {
                var error = new Error
                {
                    Date = DateTime.UtcNow,
                    ErrorMessage = exception.Message,
                    StackTrace = exception.StackTrace
                };

                var repository = context.RequestServices.GetRequiredService<IErrorsRepository>();
                await repository.Create(error);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "error",
                    message = "An unexpected exception has occurred",
                    status = 500
                });
            }
        }));
    }
}
