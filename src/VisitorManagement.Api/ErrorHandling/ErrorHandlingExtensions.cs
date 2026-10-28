using Microsoft.AspNetCore.Mvc;

namespace VisitorManagement.Api.ErrorHandling;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        // Automatic model-binding failures use the same contract.
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = ModelStateErrors.From(context.ModelState);
                var route = context.ActionDescriptor.RouteValues;
                var operation = $"{route["controller"]}.{route["action"]}";

                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ModelValidation")
                    .LogWarning("Operation={Operation} validation failed for fields {Fields}",
                        operation, string.Join(",", errors.Keys));

                var problem = ApiErrors.Create(
                    context.HttpContext, StatusCodes.Status400BadRequest,
                    "Validation failed", "One or more fields are invalid.", "validation_failed", errors);

                return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            };
        });

        return services;
    }
}