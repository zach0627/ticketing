namespace Ticketing.Domain.Users;

/// <summary>
/// 介面在 Domain、實作在 Infrastructure：聚合自己定義怎麼被存取（教學 21 §10.2）。
/// <c>Add</c> 是同步的——它只把物件交給追蹤，真正寫入在 <c>IUnitOfWork.SaveChangesAsync</c>。
/// </summary>
public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AppUser?> GetByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<AppUser?> GetByGoogleSubjectAsync(string subject, CancellationToken ct);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);
    void Add(AppUser user);
}
