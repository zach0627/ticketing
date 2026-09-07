namespace Ticketing.Application.Common;

/// <summary>分頁回應。<paramref name="Total"/> 是符合條件的總筆數，不是本頁筆數。</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);
