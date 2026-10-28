using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.Services;
using VisitorManagement.Application.Validators;
using VisitorManagement.Domain.Entities;
using VisitorManagement.Infrastructure.Persistence;
using VisitorManagement.Infrastructure.Security;
using Azure.Storage.Queues;
using VisitorManagement.Infrastructure.Messaging;

namespace VisitorManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordVerifier, PasswordVerifier>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVisitorRepository, VisitorRepository>();
        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        return services;
    }

    /// <summary>Registered by the API only. The Azure Function does not issue tokens.</summary>
    public static IServiceCollection AddJwtTokenServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(JwtOptions.Load(configuration));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        return services;
    }

    public static async Task InitialiseDatabaseAsync(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await db.Database.MigrateAsync();
        await DbSeeder.SeedUsersAsync(db, hasher);
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);   // replaceable in tests
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IVisitorService, VisitorService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddValidatorsFromAssemblyContaining<CreateVisitorRequestValidator>();
        return services;
    }

    /// <summary>Registered by the API only. The Function consumes the queue, it does not publish.</summary>
    public static IServiceCollection AddQueuePublishing(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = QueueOptions.Load(configuration);

        services.AddSingleton(_ =>
        {
            var clientOptions = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };
            clientOptions.Retry.MaxRetries = 2;                              // fail fast if the queue is unreachable
            clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(5);
            return new QueueClient(options.ConnectionString, options.QueueName, clientOptions);
        });
        services.AddScoped<IVisitorEventPublisher, QueueVisitorEventPublisher>();

        return services;
    }
}