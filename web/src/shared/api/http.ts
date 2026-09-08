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
 * access token 放 **sessionStorage**：關掉分頁就消失。
 *
 * 這不是保險箱——頁面上如果有 XSS，它照樣讀得到。
 * 它換到的只是「存活時間比較短」。真正的做法（記憶體 ＋ httpOnly cookie 的
 * refresh token）列在設計文件 17，本版沒有做。
 *
 * 每個存取都包 try/catch：無痕視窗或封鎖第三方儲存時，這些 API 會直接丟例外。
 */
const tokenKey = 'ticketing.accessToken';

export const tokenStore = {
  read(): string | null {
    try {
      return sessionStorage.getItem(tokenKey);
    } catch {
      return null;
    }
  },
  write(token: string): void {
    try {
      sessionStorage.setItem(tokenKey, token);
    } catch {
      // 存不進去就當作沒有登入狀態，重新整理後要重新登入
    }
  },
  clear(): void {
    try {
      sessionStorage.removeItem(tokenKey);
    } catch {
      // 同上
    }
  },
};

/**
 * 受保護的請求收到 401 時要通知誰。
 *
 * 為什麼不在這裡直接導向 `/login`？因為那會讓 http.ts 認識路由，
 * 而且 `/auth/login` 自己回的 401（帳密錯）也會被導走，變成無限循環。
 * 這裡只負責「說一聲」，由 AuthProvider 決定清狀態，再由 RequireAuth 決定導頁。
 */
type UnauthorizedHandler = () => void;
let onUnauthorized: UnauthorizedHandler = () => {};

export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler;
}

interface RequestOptions {
  body?: unknown;
  signal?: AbortSignal;
}

async function request<T>(method: 'GET' | 'POST', path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };

  const token = tokenStore.read();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';

  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    signal: options.signal,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  });

  if (response.ok) {
    return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
  }

  let problem: ApiProblem = {};
  try {
    problem = (await response.json()) as ApiProblem;
  } catch {
    // body 不是 JSON——保留狀態碼，不要讓解析失敗蓋掉真正的問題
  }

  // 登入端點自己回的 401 是「帳密錯」，留在表單上顯示；
  // 其他端點的 401 代表 token 沒了或過期了，才需要清狀態。
  if (response.status === 401 && !path.startsWith('/auth/')) onUnauthorized();

  throw new ApiError(
    response.status,
    problem.code ?? 'ServiceUnavailable',
    problem.detail ?? problem.title ?? `請求失敗（HTTP ${response.status}）`,
    problem.traceId,
  );
}

export const apiGet = <T>(path: string, signal?: AbortSignal): Promise<T> =>
  request<T>('GET', path, { signal });

export const apiPost = <T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> =>
  request<T>('POST', path, { body, signal });
