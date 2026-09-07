using Riok.Mapperly.Abstractions;
using Ticketing.Application.Catalog.Dtos;
using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Catalog;

/// <summary>
/// 只做**穩定欄位**的對應。含 <c>now</c> 或需要跨表計算的（卡片、活動詳情、座位圖）
/// 一律在 <c>CatalogQueries</c> 手寫 <c>Select</c>——把動態計算硬塞進對應器只會讓它變難懂
/// （設計文件 03 第 4.2 節、ADR-10）。
///
/// Mapperly 是 source generator：下面的 partial 方法在**編譯時**被填上實作，
/// 執行期沒有反射、沒有設定物件、沒有授權金鑰。
/// 產生的程式碼可以在 <c>obj/generated/</c> 看到。
///
/// <c>.editorconfig</c> 把 RMG012（目標欄位找不到來源）與 RMG020（來源欄位未使用）
/// 設成 <b>error</b>：DTO 加了欄位卻忘記對應，會編譯失敗而不是安靜送出 null。
/// </summary>
[Mapper]
public static partial class CatalogMapper
{
    /// <summary>
    /// 查詢投影：Mapperly 產生 <c>Select(s =&gt; new SectionDto { ... })</c> 的運算式，
    /// EF 只 SELECT DTO 需要的欄位，不載入整個實體。
    /// </summary>
    public static partial IQueryable<SectionDto> ProjectToSections(this IQueryable<Section> sections);

    /// <summary>單一票區的物件對應。</summary>
    [MapperIgnoreSource(nameof(Section.PerformanceId))]   // DTO 不需要：呼叫端已經知道是哪一場
    [MapperIgnoreSource(nameof(Section.Capacity))]        // 算出來的，前端自己乘
    public static partial SectionDto ToDto(Section section);
}
