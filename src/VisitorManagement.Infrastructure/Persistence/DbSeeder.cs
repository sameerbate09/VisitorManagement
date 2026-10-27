using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VisitorManagement.Domain.Constants;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Infrastructure.Persistence;

public static class DbSeeder
{
    // Demo-only credentials, documented in the README.
    public static async Task SeedUsersAsync(
        AppDbContext db, IPasswordHasher<User> hasher, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var adminRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Admin, ct);
        var userRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.User, ct);

        var admin = new User
        {
            Username = "admin",
            DisplayName = "Demo Admin",
            RoleId = adminRole.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, "Admin@12345");

        var user = new User
        {
            Username = "user",
            DisplayName = "Demo User",
            RoleId = userRole.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, "User@12345");

        db.Users.AddRange(admin, user);
        await db.SaveChangesAsync(ct);
    }
}