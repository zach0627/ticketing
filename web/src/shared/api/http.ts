import type { ApiProblem } from './types';

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080/api/v1';

/**
 * 後端回的業務錯誤。`code` 是前端判斷用的，訊息只給人看。
 */
export class ApiError extends Error {
  // 明確宣告欄位再指派：Vite 樣板開了 erasableSyntaxOnly，
  // 不允許建構式參數屬性（那是 TS 專屬的執行期語法，不能純靠型別抹除編譯）
  readonly status: number;
  readonly code: string;
  readonly traceId?: string;

  constructor(status: number, code: string, message: string, traceId?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.traceId = traceId;
  }
}

/**
 * fetch 包裝。
 *
 * 重點：**不能因為 JSON 解析失敗就遮蔽原本的 HTTP 狀態**。
 * 平台代理的 502／503、冷啟動、網路失敗都可能回非 JSON 的 body，
 * 這時仍要保留狀態碼並顯示一般服務錯誤（設計文件 13 第 3 節）。
 */
export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    signal,
    headers: { Accept: 'application/json' },
  });

  if (response.ok) {
    return (await response.json()) as T;
  }

  let problem: ApiProblem = {};
  try {
    problem = (await response.json()) as ApiProblem;
  } catch {
    // body 不是 JSON——保留狀態碼，不要讓解析失敗蓋掉真正的問題
  }

  throw new ApiError(
    response.status,
    problem.code ?? 'ServiceUnavailable',
    problem.detail ?? problem.title ?? `請求失敗（HTTP ${response.status}）`,
    problem.traceId,
  );
}
