import type { PendingOperation } from '../model/PendingOperation';

/**
 * 「還沒確定結果的那個操作」。
 *
 * 情境：使用者按下「保留」，請求送出去了，然後他重新整理、或網路斷了。
 * 這時候我們不知道伺服器到底有沒有收到——**再送一次新的請求是最糟的做法**，
 * 因為那可能變成第二次購買。
 *
 * 正確做法是把 key 記下來，重新整理後**用同一個 key 重送**：
 * 伺服器認得它，會回上一次的結果（設計文件 13 第 4 節）。
 *
 * 存 sessionStorage：關掉分頁就消失，那時改用
 * `GET /me/holds?performanceId=` 或訂單列表恢復。
 * 這不是跨裝置的儲存，也不宣稱是。
 */
const storageKey = 'ticketing.pending';

export const pendingOperations = {
  save(operation: PendingOperation): void {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(operation));
    } catch {
      // 存不進去就失去「重新整理後自動恢復」的能力，但不影響這次操作本身
    }
  },

  read(
    userId: string,
    operation: PendingOperation['operation'],
    target: string,
  ): PendingOperation | null {
    try {
      const raw = sessionStorage.getItem(storageKey);
      if (!raw) return null;

      const parsed = JSON.parse(raw) as PendingOperation;
      const matches =
        parsed.userId === userId &&
        parsed.operation === operation &&
        parsed.target === target;

      return matches ? parsed : null;
    } catch {
      return null;
    }
  },

  clear(): void {
    try {
      sessionStorage.removeItem(storageKey);
    } catch {
      // 同上
    }
  },
};
