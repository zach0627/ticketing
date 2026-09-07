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

  return (
    <div className="state state--error" role="alert">
      <p>{message}</p>
      {traceId && <p className="state__trace">traceId：{traceId}</p>}
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="state state--empty">{children}</p>;
}
