/**
 * 登入後要回哪一頁。
 *
 * **只接受本站、以單一 `/` 開頭的路徑。** 放行 `//evil.example` 或
 * `/\evil.example` 這種寫法，等於做了一個開放轉址器：
 * 攻擊者把「登入後跳到自己的釣魚站」的連結寄給使用者，網址列看起來是我們的網域
 * （設計文件 13 第 4 節）。
 */
export function safeReturnPath(candidate: unknown): string {
  if (typeof candidate !== 'string' || candidate.length === 0) return '/';
  if (!candidate.startsWith('/')) return '/';       // 絕對網址、相對路徑一律拒絕
  if (candidate.startsWith('//')) return '/';       // protocol-relative URL
  if (candidate.includes('\\')) return '/';         // 有些瀏覽器把 \ 當成 /
  return candidate;
}
