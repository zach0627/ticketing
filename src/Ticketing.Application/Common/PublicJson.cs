using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ticketing.Application.Common;

/// <summary>
/// 冪等紀錄裡存的是**最後真的送出去的那串 JSON**，所以序列化設定必須跟 API 的一致：
/// camelCase、enum 輸出字串。不一致的話，第一次的回應與重播的回應會長得不一樣
/// （設計文件 06 第 7 節）。
///
/// `Program.cs` 的 `AddJsonOptions` 要跟這裡保持同步；有一個整合測試比對
/// 「POST 回傳的欄位名」與「GET 同一筆資料的欄位名」，漂移了會紅燈。
/// </summary>
public static class PublicJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);   // 固定下來，之後不能被改
        return options;
    }
}
