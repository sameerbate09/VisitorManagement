using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Infrastructure.Persistence;

namespace VisitorManagement.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"VisitorManagement_IntTests_{Guid.NewGuid():N}";

    public CapturingPublisher Publisher { get; } = new();

    public ApiFactory()
    {
        // Program.cs reads configuration while it builds the services, so these are set as
        // environment variables, which are always applied before the host starts.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", new string('t', 48));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: the API applies migrations and seeds the demo users at startup.
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IVisitorEventPublisher>();
            services.AddSingleton<IVisitorEventPublisher>(Publisher);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeleted();
            }
            catch
            {
                // Best effort: a leftover test database can be dropped manually.
            }
        }

        base.Dispose(disposing);
    }
}