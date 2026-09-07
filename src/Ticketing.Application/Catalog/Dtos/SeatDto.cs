using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Catalog.Dtos;

/// <summary>
/// 座位圖上的一席。**不含 HoldId、不含買家**——那是別人的資料，公開端點不得外洩。
/// 過期的 Held 在投影時就被算成 <see cref="SeatStatus.Available"/>（設計文件 04 第 5 節）。
/// </summary>
public sealed record SeatDto(
    int Id,
    int SectionId,
    int RowNumber,
    int SeatNumber,
    SeatStatus Status);
