public static class CorsConfiguration
{
    public static void AddCustomCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                          ?? configuration.GetSection("AllowedOrigins").Get<string[]>()
                          ?? Array.Empty<string>();

            options.AddDefaultPolicy(config =>
            {
                // CORS is not authorization. Production must list explicit origins.
                var env = configuration["ASPNETCORE_ENVIRONMENT"]
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? "Production";
                var isDev = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);

                if (isDev || origins.Length == 0)
                {
                    if (!isDev && origins.Length == 0)
                    {
                        // Fail closed when hosting explicitly marks Production without origins.
                        // Local/demo hosts often omit the env var — keep permissive only outside Production.
                    }

                    if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase) && origins.Length == 0)
                    {
                        throw new InvalidOperationException(
                            "Cors:AllowedOrigins must be configured in Production.");
                    }

                    config.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
                else
                {
                    config.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
                }
            });

            options.AddPolicy("free", config =>
            {
                config.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
    }
}
