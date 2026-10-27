using Microsoft.EntityFrameworkCore;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Infrastructure.Persistence;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetActiveByUsernameAsync(string username, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);
}