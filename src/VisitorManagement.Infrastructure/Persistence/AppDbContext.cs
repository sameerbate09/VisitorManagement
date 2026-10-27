using Microsoft.EntityFrameworkCore;
using System.Data;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Host> Hosts => Set<Host>();
    public DbSet<Purpose> Purposes => Set<Purpose>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}