namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// SQL 連線的韌性參數。**本機與雲端的數字必須不一樣**，所以它是設定而不是常數。
///
/// 本機（預設值）面對的是「偶發的瞬時錯誤與死結」：重試兩次、兩秒內解決，
/// 解決不了就該讓它失敗，不要把一個壞掉的請求拖成十秒。
///
/// 雲端面對的是完全不同的東西：Azure SQL serverless 開了 auto-pause，
/// 閒置後整個資料庫會被收起來，喚醒要 30～90 秒。
/// 實測到的失敗**不是**文件上常提到的 40613，而是連線通過登入之後卡在 post-login，
/// 最後以連線逾時（SQL 錯誤碼 -2）結束：
///
///   Connection Timeout Expired. The timeout period elapsed during the post-login phase.
///   [Pre-Login] initialization=87; handshake=248; [Login] ...; [Post-Login] complete=29236;
///
/// 這個差別很重要，因為 40613 在 EF 的可重試清單裡，-2 不在
/// （見 <see cref="AdditionalTransientErrorNumbers"/>）。
/// 所以 Production 那組是「撐過一次冷啟動」的長度，不是「撐過一次死結」的長度，
/// 而且上限受制於 App Service 的 230 秒請求逾時：
/// 連線逾時 60 秒 × (1 + 2 次重試) ≈ 181 秒，還在裡面。
/// </summary>
public sealed class SqlResilienceOptions
{
    /// <summary>
    /// SQL 的逾時錯誤碼。**EF 預設不重試這一個，而且那個預設是對的**——
    /// 一個逾時的「命令」有可能其實已經在伺服器上執行成功了，只是回應沒回來，
    /// 盲目重試等於重複執行。所以 EF 把它留給呼叫端自己決定。
    ///
    /// 這個專案決定重試它，理由是這裡的寫入路徑已經替這個決定付過錢了：
    ///   ① 所有寫入都包在 <c>IUnitOfWork.ExecuteAsync</c> 的單一交易裡，
    ///      沒 commit 的東西不會留下痕跡；
    ///   ② ExecutionStrategy 重跑的是**整段委派**，會重新做冪等指紋比對，
    ///      連「commit 送出去但回應遺失」這種最惡劣的情況都會被判成重播
    ///      （階段六的 commit 失敗注入測試就是在證明這件事）。
    ///
    /// 而觸發這件事的是讀取：Azure SQL serverless 從 auto-pause 喚醒時，
    /// 連線會通過登入、卡在 post-login，最後以逾時（-2）失敗——
    /// 那時候一個命令都還沒送出去，重試絕對安全，但 EF 分不出來。
    /// </summary>
    public static readonly int[] AdditionalTransientErrorNumbers = [-2];

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
