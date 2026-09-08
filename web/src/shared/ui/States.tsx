import type { ReactNode } from 'react';
import { ApiError } from '../api/http';

export function Spinner({ label = '載入中…' }: { label?: string }) {
  return <p className="state state--loading" role="status">{label}</p>;
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
          如果這是你今天第一次打開，資料庫可能正在喚醒——請等幾秒再重新整理。
        </p>
      )}
      {traceId && <p className="state__trace">traceId：{traceId}</p>}
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="state state--empty">{children}</p>;
}
