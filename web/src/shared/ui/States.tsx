import { useEffect, useState, type ReactNode } from 'react';
import { ApiError } from '../api/http';

/** 實測：Azure SQL serverless 從 auto-pause 醒來要 30～60 秒（設計文件 09 第 5 節）。 */
const COLD_START_HINT_AFTER_MS = 4_000;

export function Spinner({ label = '載入中…' }: { label?: string }) {
  // 修好連線重試之後，冷啟動不再是「錯誤」，而是「一個很久的等待」——
  // 使用者看到的是一個沉默轉圈一分鐘的畫面，跟壞掉沒兩樣。
  // 所以等待超過幾秒就把原因講出來。ErrorMessage 那邊的提示照舊留著，
  // 因為連線重試耗盡（約 181 秒）之後仍然會失敗。
  const [slow, setSlow] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setSlow(true), COLD_START_HINT_AFTER_MS);
    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="state state--loading" role="status">
      <p>{label}</p>
      {slow && (
        <p className="state__hint">
          資料庫閒置後會自動暫停以節省費用，正在喚醒——大約需要一分鐘，請不要重新整理。
        </p>
      )}
    </div>
  );
}

export function ErrorMessage({ error }: { error: unknown }) {
  const message =
    error instanceof ApiError
      ? error.message
      : error instanceof Error
        ? error.message
        : '發生未預期的錯誤';

  const traceId = error instanceof ApiError ? error.traceId : undefined;

  // 雲端用的是 Azure SQL 的免費額度，閒置久了會自動暫停，第一個請求要等它醒來。
  // 這不是壞掉，但不講的話使用者只會看到一個沒有頭緒的錯誤（設計文件 09 第 2 節）。
  const mightBeColdStart =
    !(error instanceof ApiError) || error.status >= 500;

  return (
    <div className="state state--error" role="alert">
      <p>{message}</p>
      {mightBeColdStart && (
        <p className="state__hint">
          如果這是你今天第一次打開，資料庫可能正在喚醒（實測約 30～60 秒）——
          稍等一下再重新整理。
        </p>
      )}
      {traceId && <p className="state__trace">traceId：{traceId}</p>}
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="state state--empty">{children}</p>;
}
