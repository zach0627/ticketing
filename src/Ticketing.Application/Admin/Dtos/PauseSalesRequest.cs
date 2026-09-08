using System.ComponentModel.DataAnnotations;

namespace Ticketing.Application.Admin.Dtos;

public sealed record PauseSalesRequest
{
    [Required]
    public bool IsSalesPaused { get; init; }
}

/// <summary>回傳操作後的實際狀態，前端不必猜。</summary>
public sealed record PauseSalesResult(int PerformanceId, bool IsSalesPaused);
