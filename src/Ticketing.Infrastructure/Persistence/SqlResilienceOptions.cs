namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// SQL 連線的韌性參數。**本機與雲端的數字必須不一樣**，所以它是設定而不是常數。
///
/// 本機（預設值）面對的是「偶發的瞬時錯誤與死結」：重試兩次、兩秒內解決，
/// 解決不了就該讓它失敗，不要把一個壞掉的請求拖成十秒。
///
/// 雲端面對的是完全不同的東西：Azure SQL serverless 開了 auto-pause，
/// 閒置後整個資料庫會被收起來，下一次連線會拿到 <c>40613 Database is not currently available</c>，
/// 而喚醒要 30～90 秒。用本機那組數字，閒置後第一個訪客一定看到 500。
/// 所以 Production 那組是「撐過一次冷啟動」的長度，不是「撐過一次死結」的長度。
/// </summary>
public sealed class SqlResilienceOptions
{
    /// <summary>
    /// 單一語句的上限。這個數字同時是**鎖等待的上限**——防超賣的三道 gate 會持鎖，
    /// 放太寬等於讓一個卡住的請求擋住整個場次，所以雲端也只從 10 放寬到 15，
    /// 讓 serverless 剛喚醒、buffer pool 還是空的那批查詢有機會打完磁碟。
    /// </summary>
    public int CommandTimeoutSeconds { get; init; } = 10;

    /// <summary>重試次數。EF 的退避是指數型的，所以次數比延遲更能決定總長度。</summary>
    public int MaxRetryCount { get; init; } = 2;

    /// <summary>單次重試的延遲上限（指數退避封頂在這裡）。</summary>
    public int MaxRetryDelaySeconds { get; init; } = 2;
}
