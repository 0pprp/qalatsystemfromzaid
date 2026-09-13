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

            if (exception is null)
            {
                return;
            }

            // Persistence must never mask or replace the original failure (e.g. missing Errors_Create on Demo).
            try
            {
                var repository = context.RequestServices.GetRequiredService<IErrorsRepository>();
                await repository.Create(new Error
                {
                    Date = DateTime.UtcNow,
                    ErrorMessage = exception.Message,
                    StackTrace = exception.StackTrace
                });
            }
            catch (Exception persistEx)
            {
                var logger = context.RequestServices
                    .GetService<ILoggerFactory>()
                    ?.CreateLogger("GlobalExceptionHandler");
                logger?.LogError(
                    persistEx,
                    "Failed to persist exception to Errors store. Original: {OriginalMessage}",
                    exception.Message);
            }

            if (!context.Response.HasStarted)
            {
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
