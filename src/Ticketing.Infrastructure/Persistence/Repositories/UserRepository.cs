using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(TicketingDbContext db) : IUserRepository
{
    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<AppUser?> GetByEmailAsync(string normalizedEmail, CancellationToken ct)
        => db.AppUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<AppUser?> GetByGoogleSubjectAsync(string subject, CancellationToken ct)
        => db.AppUsers.FirstOrDefaultAsync(u => u.GoogleSubject == subject, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct)
        => db.AppUsers.AsNoTracking().AnyAsync(u => u.Email == normalizedEmail, ct);

    public void Add(AppUser user) => db.AppUsers.Add(user);
}
