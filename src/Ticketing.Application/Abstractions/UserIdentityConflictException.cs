namespace Ticketing.Application.Abstractions;

/// <summary>
/// 使用者身份的唯一索引被撞到了。
///
/// 這是**可恢復的邊界**：Infrastructure 認得 <c>UQ_AppUsers_Email</c> 與
/// <c>UX_AppUsers_GoogleSubject</c> 兩個索引，把它們翻譯成這個型別；
/// Application 才知道要重讀、要回 409 還是要往上拋（設計文件 05 第 4.1 節）。
///
/// 為什麼不讓 SQL 例外直接穿到 Application？因為那等於 Application 認識了資料庫的錯誤碼——
/// 換一種資料庫就得改流程，而且 2601／2627 不只會由這兩個索引產生。
/// </summary>
public sealed class UserIdentityConflictException(UserIdentityConflict conflict, Exception inner)
    : Exception($"使用者身份衝突：{conflict}", inner)
{
    public UserIdentityConflict Conflict { get; } = conflict;
}

public enum UserIdentityConflict
{
    /// <summary>Email 已被其他帳號使用。</summary>
    Email,

    /// <summary>同一個 Google subject 已經有帳號了——通常是兩個併發的初次登入。</summary>
    GoogleSubject
}
