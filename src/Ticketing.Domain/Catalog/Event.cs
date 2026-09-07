using Ticketing.Domain.Common;

namespace Ticketing.Domain.Catalog;

/// <summary>
/// 一個演唱會或一場比賽的內容。沒有時間規則——售票窗口在 <see cref="Performance"/>。
/// 下架用 <see cref="IsPublished"/>，不真刪（設計文件 04 第 7 節）。
/// </summary>
public sealed class Event
{
    public int Id { get; private set; }
    public string Code { get; private set; } = "";          // C01…C12、S01…S03
    public EventCategory Category { get; private set; }
    public string Title { get; private set; } = "";
    public string Performer { get; private set; } = "";
    public string Genre { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string ImagePath { get; private set; } = "";     // /assets/events/C01.png
    public bool IsPublished { get; private set; }

    private Event() { }                                     // EF Core 用

    public Event(int id, string code, EventCategory category, string title,
                 string performer, string genre, string description, string imagePath,
                 bool isPublished = true)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "活動的 Id、Code 與標題不可為空");

        Id = id;
        Code = code;
        Category = category;
        Title = title;
        Performer = performer;
        Genre = genre;
        Description = description;
        ImagePath = imagePath;
        IsPublished = isPublished;
    }

    public void Unpublish() => IsPublished = false;
    public void Publish() => IsPublished = true;
}
