using VisitorManagement.Contracts;

namespace VisitorManagement.Api.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "Client";

    public static IServiceCollection AddClientCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)                                   // explicit list, no wildcard
            .WithMethods("GET", "POST", "PUT")
            .WithHeaders("Authorization", "Content-Type", CorrelationConstants.HeaderName)
            .WithExposedHeaders(CorrelationConstants.HeaderName))); // lets the browser read the ID

        return services;
    }
}