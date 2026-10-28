using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using VisitorManagement.Api.Authorization;
using VisitorManagement.Api.ErrorHandling;
using VisitorManagement.Domain.Constants;
using VisitorManagement.Infrastructure.Security;

namespace VisitorManagement.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = JwtOptions.Load(configuration);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;   // keep the short claim names: sub, name, role
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                options.Events = BuildEvents();
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CanViewVisitors,
                p => p.RequireRole(RoleNames.Admin, RoleNames.User));
            options.AddPolicy(Policies.CanManageVisitors,
                p => p.RequireRole(RoleNames.Admin));

            // Secure by default: every endpoint needs a valid token unless marked [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    private static JwtBearerEvents BuildEvents() => new()
    {
        OnAuthenticationFailed = context =>
        {
            // Log the failure type only. The token itself is never logged.
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication")
                .LogWarning("Operation={Operation} failed: {Reason}", "ValidateToken", context.Exception.GetType().Name);
            return Task.CompletedTask;
        },

        OnChallenge = async context =>
        {
            context.HandleResponse();
            if (context.Response.HasStarted) return;

            var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
            var problem = ApiErrors.Create(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                expired
                    ? "Your session has expired. Please sign in again."
                    : "Authentication is required. Please sign in.",
                expired ? "token_expired" : "unauthorized");

            context.Response.Headers.WWWAuthenticate = "Bearer";
            await ApiErrors.WriteAsync(context.HttpContext, problem, context.HttpContext.RequestAborted);
        },

        OnForbidden = async context =>
        {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authorization")
                .LogWarning("Operation={Operation} forbidden for user {UserId} on {Method} {Path}",
                    "Authorize",
                    context.HttpContext.User.FindFirst("sub")?.Value,
                    context.Request.Method,
                    context.Request.Path.Value);

            var problem = ApiErrors.Create(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You do not have permission to perform this action.",
                "forbidden");

            await ApiErrors.WriteAsync(context.HttpContext, problem, context.HttpContext.RequestAborted);
        }
    };
}